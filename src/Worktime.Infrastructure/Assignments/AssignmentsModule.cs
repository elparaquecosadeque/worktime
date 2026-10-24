using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.Assignments.Interfaces;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Assignments;

public static class AssignmentsModule
{
    public static IServiceCollection AddAssignmentsModule(this IServiceCollection services, ReadsAdapter reads) => services
        .AddScoped<IAssignmentRequestRepository, AssignmentRequestRepository>()
        .AddReads<IAssignmentReads, EfAssignmentReads, DapperAssignmentReads>(reads);
}
