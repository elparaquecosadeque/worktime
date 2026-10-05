using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

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

/// <summary>Builds <c>perm:a|b</c> policies on demand, so no policy has to be registered per permission.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "perm:";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(Prefix, StringComparison.Ordinal)) return await base.GetPolicyAsync(policyName);
        var permissions = policyName[Prefix.Length..].Split('|', StringSplitOptions.RemoveEmptyEntries);
        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(WorktimeClaims.Permission, permissions)
            .Build();
    }
}
