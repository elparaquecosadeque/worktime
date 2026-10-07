using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Presence;
using Worktime.Application.Users;
using Worktime.Application.WorkLogs;

namespace Worktime.Application;

public static class DependencyInjection
{
    /// <summary>Application services that are not Mediator handlers (those are source-generated in the host).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services) => services
        .AddSingleton<KeyedLock>()
        .AddSingleton<WorktimeMetrics>()
        .AddScoped<IDomainEventDispatcher, RealtimeEventDispatcher>()
        .AddScoped<Audience>()
        .AddScoped<UserScope>()
        .AddScoped<WorkLogDecider>();
}
