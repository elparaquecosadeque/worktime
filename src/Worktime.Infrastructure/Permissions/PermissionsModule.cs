using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.Permissions.Interfaces;

namespace Worktime.Infrastructure.Permissions;

public static class PermissionsModule
{
    public static IServiceCollection AddPermissionsModule(this IServiceCollection services) => services
        .AddScoped<IRolePermissionRepository, RolePermissionRepository>();
}
