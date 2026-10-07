using System.Text.Json;
using StackExchange.Redis;
using Worktime.Application.Common.Interfaces;

namespace Worktime.Infrastructure.Redis;

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
