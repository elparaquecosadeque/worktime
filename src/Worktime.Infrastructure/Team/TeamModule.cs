using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.Team.Interfaces;

namespace Worktime.Infrastructure.Team;

public static class TeamModule
{
    public static IServiceCollection AddTeamModule(this IServiceCollection services) => services
        .AddScoped<ITeamReads, TeamReads>();
}
