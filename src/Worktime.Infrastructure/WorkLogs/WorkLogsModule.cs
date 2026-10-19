using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.WorkLogs.Interfaces;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.WorkLogs;

public static class WorkLogsModule
{
    public static IServiceCollection AddWorkLogsModule(this IServiceCollection services, ReadsAdapter reads) => services
        .AddScoped<IWorkLogRepository, WorkLogRepository>()
        .AddReads<IWorkLogReads, EfWorkLogReads, DapperWorkLogReads>(reads);
}
