using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Worktime.Application.Common;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Redis;

internal static class Keys
{
    public const string Stamps = "worktime:stamps";          // hash userId -> stamp
    public const string PresenceSeen = "worktime:presence:seen"; // zset userId -> last heartbeat (unix ms)
    public const string PresenceConns = "worktime:presence:conns"; // hash userId -> live connection count
    public static string Cache(string key) => $"worktime:cache:{key}";
}

/// <summary>Short-lived shared cache (team summary). Shared across replicas, so a reconnect storm is one DB hit per TTL.</summary>
internal sealed class RedisCacheStore(IConnectionMultiplexer redis) : ICacheStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync(Keys.Cache(key));
        if (cached.HasValue) return JsonSerializer.Deserialize<T>(cached.ToString(), Json)!;

        // ponytail: no stampede lock; concurrent misses each compute once per TTL, fine at 5 s.
        var value = await factory(ct);
        await db.StringSetAsync(Keys.Cache(key), JsonSerializer.Serialize(value, Json), ttl);
        return value;
    }
}

internal sealed class RedisStampStore(IConnectionMultiplexer redis) : IStampStore
{
    public Task SetAsync(Guid userId, string stamp, CancellationToken ct) =>
        redis.GetDatabase().HashSetAsync(Keys.Stamps, userId.ToString(), stamp);
}

/// <summary>
/// Runs on every authenticated request (JWT OnTokenValidated). Memory (2 s) → Redis → Postgres fallback,
/// so the happy path costs no DB query and revocation still lands within ~2 s on every replica.
/// </summary>
public sealed class StampValidator(IConnectionMultiplexer redis, IMemoryCache memory, IServiceScopeFactory scopes)
{
    public async Task<bool> IsCurrentAsync(Guid userId, string stamp, CancellationToken ct)
    {
        var current = await memory.GetOrCreateAsync($"stamp:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(2);
            var db = redis.GetDatabase();
            var fromRedis = await db.HashGetAsync(Keys.Stamps, userId.ToString());
            if (fromRedis.HasValue) return fromRedis.ToString();

            // Redis lost it (restart, demo reset): ask the source of truth. Unknown or inactive users get "".
            await using var scope = scopes.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<WorktimeDbContext>().Users.AsNoTracking();
            var fromDb = await users.Where(u => u.Id == userId && u.IsActive).Select(u => u.SecurityStamp).FirstOrDefaultAsync(ct) ?? "";
            if (fromDb != "") await db.HashSetAsync(Keys.Stamps, userId.ToString(), fromDb);
            return fromDb;
        });
        return current == stamp;
    }
}

/// <summary>
/// Presence that survives a dead replica: a sorted set of last heartbeats (online = seen within the TTL)
/// plus a per-user connection counter so closing one of several tabs does not flicker offline.
/// A replica that dies leaves its counters inflated; the sweep resets them once heartbeats stop.
/// </summary>
internal sealed class RedisPresenceStore(IConnectionMultiplexer redis, IClock clock) : IPresenceStore
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private IDatabase Db => redis.GetDatabase();
    private double Now => clock.UtcNow.ToUnixTimeMilliseconds();

    public async Task<bool> ConnectAsync(Guid userId, CancellationToken ct)
    {
        var connections = await Db.HashIncrementAsync(Keys.PresenceConns, userId.ToString());
        await Db.SortedSetAddAsync(Keys.PresenceSeen, userId.ToString(), Now);
        return connections == 1;
    }

    public async Task<bool> DisconnectAsync(Guid userId, CancellationToken ct)
    {
        var connections = await Db.HashDecrementAsync(Keys.PresenceConns, userId.ToString());
        if (connections > 0) return false;
        await Db.HashDeleteAsync(Keys.PresenceConns, userId.ToString());
        await Db.SortedSetRemoveAsync(Keys.PresenceSeen, userId.ToString());
        return true;
    }

    public Task HeartbeatAsync(Guid userId, CancellationToken ct) =>
        Db.SortedSetAddAsync(Keys.PresenceSeen, userId.ToString(), Now);

    public async Task<IReadOnlySet<Guid>> GetOnlineAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return new HashSet<Guid>();
        var ids = userIds.ToArray();
        var scores = await Db.SortedSetScoresAsync(Keys.PresenceSeen, ids.Select(i => (RedisValue)i.ToString()).ToArray());
        var cutoff = Now - Ttl.TotalMilliseconds;
        return ids.Where((_, i) => scores[i] is { } s && s >= cutoff).ToHashSet();
    }

    public async Task<IReadOnlyList<Guid>> SweepExpiredAsync(CancellationToken ct)
    {
        var expired = await Db.SortedSetRangeByScoreAsync(Keys.PresenceSeen, double.NegativeInfinity, Now - Ttl.TotalMilliseconds);
        var removed = new List<Guid>();
        foreach (var member in expired)
        {
            // ZREM's result makes the sweep idempotent if two instances ever overlap.
            if (!await Db.SortedSetRemoveAsync(Keys.PresenceSeen, member)) continue;
            await Db.HashDeleteAsync(Keys.PresenceConns, member);
            removed.Add(Guid.Parse(member.ToString()));
        }
        return removed;
    }
}
