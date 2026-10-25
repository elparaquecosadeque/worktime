using Worktime.Application.Tenants.Results;

namespace Worktime.Application.Tenants.Interfaces;

/// <summary>Tenant lookups for host resolution, briefly cached: they run on every request and tenants rarely change.</summary>
public interface ITenantDirectory
{
    Task<TenantInfo?> FindBySlugAsync(string slug, CancellationToken ct);
    Task<TenantInfo?> GetAsync(Guid id, CancellationToken ct);
}
