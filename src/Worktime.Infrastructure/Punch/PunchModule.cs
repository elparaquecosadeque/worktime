using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.Punch.Interfaces;

namespace Worktime.Infrastructure.Punch;

public static class PunchModule
{
    public static IServiceCollection AddPunchModule(this IServiceCollection services) => services
        .AddScoped<IPunchRepository, PunchRepository>();
}
