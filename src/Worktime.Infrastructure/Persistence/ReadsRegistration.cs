using Microsoft.Extensions.DependencyInjection;

namespace Worktime.Infrastructure.Persistence;

public static class ReadsRegistration
{
    /// <summary>Registers the EF or the Dapper implementation of a read port, as configured.</summary>
    public static IServiceCollection AddReads<TService, TEf, TDapper>(this IServiceCollection services, ReadsAdapter reads)
        where TService : class
        where TEf : class, TService
        where TDapper : class, TService =>
        reads == ReadsAdapter.Dapper
            ? services.AddScoped<TService, TDapper>()
            : services.AddScoped<TService, TEf>();
}
