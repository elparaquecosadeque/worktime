using StackExchange.Redis;
using Worktime.Application.Common.Interfaces;

namespace Worktime.Infrastructure.Redis;

internal sealed class RedisStampStore(IConnectionMultiplexer redis) : IStampStore
{
    public Task SetAsync(Guid userId, string stamp, CancellationToken ct) =>
        redis.GetDatabase().HashSetAsync(Keys.Stamps, userId.ToString(), stamp);
}
