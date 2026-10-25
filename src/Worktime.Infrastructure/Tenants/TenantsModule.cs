using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Tenants.Interfaces;

namespace Worktime.Infrastructure.Tenants;

public static class TenantsModule
{
    public static IServiceCollection AddTenantsModule(this IServiceCollection services) => services
        .AddScoped<ITenantContext, TenantContext>()
        .AddScoped<ITenantDirectory, TenantDirectory>();
}
