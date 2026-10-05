using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using Worktime.Application.Auth;
using Worktime.Application.Common;
using Worktime.Application.Team;
using Worktime.Infrastructure.Assignments;
using Worktime.Infrastructure.Permissions;
using Worktime.Infrastructure.Persistence;
using Worktime.Infrastructure.Punch;
using Worktime.Infrastructure.Redis;
using Worktime.Infrastructure.Seeding;
using Worktime.Infrastructure.Team;
using Worktime.Infrastructure.Users;
using Worktime.Infrastructure.WorkLogs;

namespace Worktime.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Cross-module plumbing: DbContext, unit of work, Redis, clock, seeding and background jobs.</summary>
    public static IServiceCollection AddSharedKernel(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<WorktimeDbContext>(o => o
            .UseNpgsql(config.GetConnectionString("Postgres"))
            .UseSnakeCaseNamingConvention());

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(config.GetConnectionString("Redis")!));
        services.AddMemoryCache();

        services.Configure<SeedOptions>(config.GetSection(SeedOptions.Section));
        services.Configure<CacheOptions>(config.GetSection(CacheOptions.Section));

        services
            .AddSingleton<IClock, SystemClock>()
            .AddScoped<IUnitOfWork, UnitOfWork>()
            .AddSingleton<ICacheStore, RedisCacheStore>()
            .AddSingleton<IStampStore, RedisStampStore>()
            .AddSingleton<IPresenceStore, RedisPresenceStore>()
            .AddSingleton<StampValidator>()
            .AddScoped<DemoSeeder>()
            .AddScoped<IDemoAccounts, DemoAccounts>()
            .AddHostedService<DatabaseInitializer>()
            .AddHostedService<DemoResetService>()
            .AddHostedService<PresenceSweepService>();

        services.AddHealthChecks()
            .AddDbContextCheck<WorktimeDbContext>("postgres")
            .AddCheck<RedisHealthCheck>("redis");

        return services;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config) => services
        .AddSharedKernel(config)
        .AddUsersModule()
        .AddPermissionsModule()
        .AddPunchModule()
        .AddWorkLogsModule()
        .AddAssignmentsModule()
        .AddTeamModule();
}

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

internal sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var latency = await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"ping {latency.TotalMilliseconds:0.0}ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("redis unreachable", ex);
        }
    }
}
