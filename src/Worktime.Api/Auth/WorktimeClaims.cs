using System.IdentityModel.Tokens.Jwt;

namespace Worktime.Api.Auth;

public static class WorktimeClaims
{
    public const string Subject = JwtRegisteredClaimNames.Sub;
    public const string Name = "name";
    public const string Email = "email";
    public const string Role = "role";
    public const string Stamp = "stamp";
    public const string Permission = "perm";
    /// <summary>Must match the tenant of the request's Host (checked on every request, see Program.AddAuth).</summary>
    public const string TenantId = "tenant_id";
    /// <summary>Informative for the UI only: never trusted for authorization (it goes stale on reassignment).</summary>
    public const string SupervisorId = "supervisor_id";
}
