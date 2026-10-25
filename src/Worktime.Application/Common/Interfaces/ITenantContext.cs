namespace Worktime.Application.Common.Interfaces;

/// <summary>
/// The tenant this scope works for: set from the Host by the API, from the token claims by the hub, and explicitly by
/// background jobs. <see cref="Guid.Empty"/> means no tenant (root host): every tenant-owned query then matches nothing.
/// </summary>
public interface ITenantContext
{
    Guid Id { get; }
    void Use(Guid tenantId);
}
