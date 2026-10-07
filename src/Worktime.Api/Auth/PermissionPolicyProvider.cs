using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Worktime.Api.Auth;

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
