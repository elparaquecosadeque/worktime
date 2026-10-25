using System.Security.Claims;
using Worktime.Application.Common;
using Worktime.Domain.Users;

namespace Worktime.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid UserId(this ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(WorktimeClaims.Subject)!);

    /// <summary>Guid.Empty for tokens issued before multitenancy: they match no tenant and are rejected.</summary>
    public static Guid TenantId(this ClaimsPrincipal p) => Guid.TryParse(p.FindFirstValue(WorktimeClaims.TenantId), out var id) ? id : Guid.Empty;

    public static Actor ToActor(this ClaimsPrincipal p) => new(
        p.UserId(),
        Enum.Parse<Role>(p.FindFirstValue(WorktimeClaims.Role)!),
        p.FindAll(WorktimeClaims.Permission).Select(c => c.Value).ToHashSet());
}
