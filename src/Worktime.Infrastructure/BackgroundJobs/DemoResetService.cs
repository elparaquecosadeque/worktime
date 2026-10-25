using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Presence;
using Worktime.Infrastructure.Persistence;
using Worktime.Infrastructure.Redis;
using Worktime.Infrastructure.Seeding;

namespace Worktime.Infrastructure.BackgroundJobs;

/// <summary>
/// Wipes and reseeds each demo tenant once its data is Seed:ResetInterval old. Every replica checks periodically;
/// the advisory lock plus the data-age check make exactly one of them do it, and restarts don't postpone it.
/// Only <see cref="DemoTenants"/> are touched: real tenants share the tables and are never wiped.
/// </summary>
public sealed class DemoResetService(IServiceScopeFactory scopes, IOptions<SeedOptions> options, ILogger<DemoResetService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled) return;
        // Check at most hourly (PeriodicTimer also rejects periods beyond ~49 days).
        var check = options.Value.ResetInterval < TimeSpan.FromHours(1) ? options.Value.ResetInterval : TimeSpan.FromHours(1);
        using var timer = new PeriodicTimer(check);
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                if (await ResetAsync(ct)) logger.LogInformation("Demo reset completed");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Demo reset failed");
            }
        }
    }

    public async Task<bool> ResetAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<WorktimeDbContext>();
        var tenant = sp.GetRequiredService<ITenantContext>();
        var seeder = sp.GetRequiredService<DemoSeeder>();
        var now = sp.GetRequiredService<IClock>().UtcNow;
        var reset = new List<Guid>();
        var removedUsers = new List<Guid>();

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            if (!await LockKeys.TryXactLockAsync(db, LockKeys.DemoReset, ct)) return false;
            foreach (var (id, _, _) in DemoTenants.All)
            {
                tenant.Use(id);
                var seededAt = await db.Users.Where(u => u.Email == DemoSeeder.AdminEmail).Select(u => (DateTimeOffset?)u.CreatedAt).FirstOrDefaultAsync(ct);
                if (seededAt > now - options.Value.ResetInterval) continue; // still fresh (or another replica just did it)

                removedUsers.AddRange(await db.Users.Select(u => u.Id).ToListAsync(ct));
                await DemoSeeder.ClearTenantAsync(db, id, ct);
                await seeder.SeedTenantAsync(db, now, ct);
                reset.Add(id);
            }
            await tx.CommitAsync(ct);
        }
        if (reset.Count == 0) return false;

        // Stamps and presence are keyed by user id: drop only the users that no longer exist.
        var redis = sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var members = removedUsers.Select(u => (RedisValue)u.ToString()).ToArray();
        await redis.HashDeleteAsync(Keys.Stamps, members);
        await redis.SortedSetRemoveAsync(Keys.PresenceSeen, members);
        await redis.HashDeleteAsync(Keys.PresenceConns, members);
        await sp.GetRequiredService<IRealtimeNotifier>()
            .SendAsync(reset.Select(RealtimeGroups.Tenant).ToList(), RealtimeEvents.DemoReset, new { at = now }, ct);
        return true;
    }
}
