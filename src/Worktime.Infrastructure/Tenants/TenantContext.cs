using Worktime.Application.Common.Interfaces;

namespace Worktime.Infrastructure.Tenants;

internal sealed class TenantContext : ITenantContext
{
    public Guid Id { get; private set; }

    public void Use(Guid tenantId) => Id = tenantId;
}
