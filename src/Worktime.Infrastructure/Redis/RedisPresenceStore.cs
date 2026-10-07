using StackExchange.Redis;
using Worktime.Application.Common.Interfaces;

namespace Worktime.Infrastructure.Redis;

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
