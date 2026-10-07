using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Presence;
using Worktime.Infrastructure.Persistence;
using Worktime.Infrastructure.Redis;
using Worktime.Infrastructure.Seeding;

namespace Worktime.Infrastructure.BackgroundJobs;

/// <summary>
/// Wipes and reseeds the demo once the data is Seed:ResetInterval old. Every replica checks periodically;
/// the advisory lock plus the data-age check make exactly one of them do it, and restarts don't postpone it.
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
        var now = sp.GetRequiredService<IClock>().UtcNow;

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            if (!await LockKeys.TryXactLockAsync(db, LockKeys.DemoReset, ct)) return false;
            var seededAt = await db.Users.Where(u => u.Email == DemoSeeder.AdminEmail).Select(u => (DateTimeOffset?)u.CreatedAt).FirstOrDefaultAsync(ct);
            if (seededAt > now - options.Value.ResetInterval) return false; // still fresh (or another replica just did it)

            await DemoSeeder.ClearAsync(db, ct);
            await sp.GetRequiredService<DemoSeeder>().SeedAsync(db, now, ct);
            await tx.CommitAsync(ct);
        }

        var redis = sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        await redis.KeyDeleteAsync([Keys.Stamps, Keys.PresenceSeen, Keys.PresenceConns]);
        await sp.GetRequiredService<IRealtimeNotifier>().BroadcastAsync(RealtimeEvents.DemoReset, new { at = now }, ct);
        return true;
    }
}
