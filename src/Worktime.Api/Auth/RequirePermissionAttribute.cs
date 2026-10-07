using Microsoft.AspNetCore.Authorization;

namespace Worktime.Api.Auth;

/// <summary>
/// <c>[RequirePermission(Perms.WorkLogsApprove)]</c> — capability check against the token's <c>perm</c> claims.
/// Several permissions mean "any of". Built on AuthorizeAttribute (not an action filter) so it runs before
/// model binding, yields correct 401/403, and works the same on controllers and SignalR hub methods.
/// Ownership ("is this *your* worker?") is not a capability: the domain/handlers decide it.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(params string[] permissions)
    : AuthorizeAttribute(PermissionPolicyProvider.Prefix + string.Join('|', permissions));
