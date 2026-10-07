using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Worktime.Application.Common.Interfaces;
using Worktime.Infrastructure.Persistence;
using Worktime.Infrastructure.Seeding;

namespace Worktime.Infrastructure.BackgroundJobs;

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
