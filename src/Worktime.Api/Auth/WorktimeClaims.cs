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
    /// <summary>Informative for the UI only: never trusted for authorization (it goes stale on reassignment).</summary>
    public const string SupervisorId = "supervisor_id";
}
