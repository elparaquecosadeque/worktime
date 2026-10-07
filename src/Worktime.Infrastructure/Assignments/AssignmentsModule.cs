using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.Assignments.Interfaces;

namespace Worktime.Infrastructure.Assignments;

public static class AssignmentsModule
{
    public static IServiceCollection AddAssignmentsModule(this IServiceCollection services) => services
        .AddScoped<IAssignmentRequestRepository, AssignmentRequestRepository>()
        .AddScoped<IAssignmentReads, AssignmentReads>();
}
