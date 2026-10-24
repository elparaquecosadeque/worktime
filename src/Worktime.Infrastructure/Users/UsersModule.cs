using Microsoft.Extensions.DependencyInjection;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Users.Interfaces;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Users;

public static class UsersModule
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services, ReadsAdapter reads) => services
        .AddScoped<IUserRepository, UserRepository>()
        .AddReads<IUserReads, EfUserReads, DapperUserReads>(reads)
        .AddScoped<IPasswordHasher, PasswordHasherAdapter>();
}
