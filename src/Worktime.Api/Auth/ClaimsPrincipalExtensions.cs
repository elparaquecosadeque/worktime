using System.Security.Claims;
using Worktime.Application.Common;
using Worktime.Domain.Users;

namespace Worktime.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid UserId(this ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(WorktimeClaims.Subject)!);

    public static Actor ToActor(this ClaimsPrincipal p) => new(
        p.UserId(),
        Enum.Parse<Role>(p.FindFirstValue(WorktimeClaims.Role)!),
        p.FindAll(WorktimeClaims.Permission).Select(c => c.Value).ToHashSet());
}
