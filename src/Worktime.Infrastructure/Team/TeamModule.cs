using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.Team.Interfaces;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Team;

public static class TeamModule
{
    public static IServiceCollection AddTeamModule(this IServiceCollection services, ReadsAdapter reads) => services
        .AddReads<ITeamReads, EfTeamReads, DapperTeamReads>(reads);
}
