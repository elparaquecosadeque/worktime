using Worktime.Domain.Tenants;

namespace Worktime.Application.Tenants.Results;

public sealed record TenantInfo(Guid Id, string Name, string Slug, TenantStatus Status, bool IsDemo);
