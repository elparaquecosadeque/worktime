using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Redis;

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
