using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.WorkLogs.Interfaces;

namespace Worktime.Infrastructure.WorkLogs;

public static class WorkLogsModule
{
    public static IServiceCollection AddWorkLogsModule(this IServiceCollection services) => services
        .AddScoped<IWorkLogRepository, WorkLogRepository>()
        .AddScoped<IWorkLogReads, WorkLogReads>();
}
