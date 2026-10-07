using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Users.Interfaces;

namespace Worktime.Infrastructure.Users;

public static class UsersModule
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services) => services
        .AddScoped<IUserRepository, UserRepository>()
        .AddScoped<IUserReads, UserReads>()
        .AddScoped<IPasswordHasher, PasswordHasherAdapter>();
}
