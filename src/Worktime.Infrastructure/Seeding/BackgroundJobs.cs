using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Worktime.Application.Common;
using Worktime.Application.Presence;
using Worktime.Infrastructure.Persistence;
using Worktime.Infrastructure.Redis;

namespace Worktime.Infrastructure.Seeding;

/// <summary>Postgres advisory-lock keys: one replica at a time does each job.</summary>
internal static class LockKeys
{
    public const long Migrate = 7_001;
    public const long DemoReset = 7_002;
    public const long PresenceSweep = 7_003;

    /// <summary>Transaction-scoped try-lock: released automatically on commit/rollback, never leaks.</summary>
    public static async Task<bool> TryXactLockAsync(WorktimeDbContext db, long key, CancellationToken ct) =>
        await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock({key}) AS \"Value\"").SingleAsync(ct);
}

/// <summary>Migrates and seeds before the host starts serving. Replicas queue on a session advisory lock.</summary>
public sealed class DatabaseInitializer(IServiceScopeFactory scopes, IOptions<SeedOptions> seed, ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WorktimeDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        await db.Database.OpenConnectionAsync(ct); // same connection for lock, migrate and unlock
        try
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_lock({LockKeys.Migrate})", ct);
            await db.Database.MigrateAsync(ct);
            if (seed.Value.Enabled && !await db.Users.AnyAsync(ct))
            {
                await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync(db, clock.UtcNow, ct);
                logger.LogInformation("Demo data seeded");
            }
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({LockKeys.Migrate})", CancellationToken.None);
            await db.Database.CloseConnectionAsync();
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

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

/// <summary>Emits "offline" for users whose heartbeat expired (crashed replica, lost network).</summary>
public sealed class PresenceSweepService(IServiceScopeFactory scopes, ILogger<PresenceSweepService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<WorktimeDbContext>();
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                if (!await LockKeys.TryXactLockAsync(db, LockKeys.PresenceSweep, ct)) continue;
                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new SweepPresenceCommand(), ct);
                await tx.CommitAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Presence sweep failed");
            }
        }
    }
}
