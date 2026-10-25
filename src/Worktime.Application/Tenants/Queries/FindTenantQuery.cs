using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Tenants.Interfaces;
using Worktime.Application.Tenants.Results;
using Worktime.Domain.Tenants;

namespace Worktime.Application.Tenants.Queries;

/// <summary>"Which space do you want to enter?": a tenant still waiting for its first payment does not exist yet.</summary>
public sealed record FindTenantQuery(string Slug) : IQuery<TenantInfo>;

public sealed class FindTenantHandler(ITenantDirectory directory) : IQueryHandler<FindTenantQuery, TenantInfo>
{
    public async ValueTask<TenantInfo> Handle(FindTenantQuery q, CancellationToken ct) =>
        await directory.FindBySlugAsync(q.Slug.Trim().ToLowerInvariant(), ct) is { Status: not TenantStatus.PendingPayment } tenant
            ? tenant
            : throw new NotFoundException("tenant.not_found");
}
