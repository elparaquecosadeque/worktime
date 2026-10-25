using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Tenants.Interfaces;
using Worktime.Application.Tenants.Results;

namespace Worktime.Application.Tenants.Queries;

/// <summary>The tenant the request's Host resolved to; on the root host (or an unknown slug) there is none.</summary>
public sealed record CurrentTenantQuery : IQuery<TenantInfo>;

public sealed class CurrentTenantHandler(ITenantContext tenant, ITenantDirectory directory) : IQueryHandler<CurrentTenantQuery, TenantInfo>
{
    public async ValueTask<TenantInfo> Handle(CurrentTenantQuery q, CancellationToken ct) =>
        await directory.GetAsync(tenant.Id, ct) ?? throw new NotFoundException("tenant.not_found");
}
