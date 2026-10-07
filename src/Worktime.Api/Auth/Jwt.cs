using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Domain.Users;

namespace Worktime.Api.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "worktime";
    public string Audience { get; set; } = "worktime-web";
    /// <summary>Shared by every replica (configuration, not generated), so any replica validates any token.</summary>
    public string SigningKey { get; set; } = "";
    public int LifetimeHours { get; set; } = 8;

    public SymmetricSecurityKey Key => new(Encoding.UTF8.GetBytes(SigningKey));
}

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

public sealed class JwtTokenIssuer(IOptions<JwtOptions> options, IClock clock) : ITokenIssuer
{
    public IssuedToken Issue(User user, IReadOnlyCollection<string> permissions)
    {
        var o = options.Value;
        var expires = clock.UtcNow.AddHours(o.LifetimeHours);
        var claims = new List<Claim>
        {
            new(WorktimeClaims.Subject, user.Id.ToString()),
            new(WorktimeClaims.Name, user.Name),
            new(WorktimeClaims.Email, user.Email),
            new(WorktimeClaims.Role, user.Role.ToString()),
            new(WorktimeClaims.Stamp, user.SecurityStamp),
        };
        claims.AddRange(permissions.Select(p => new Claim(WorktimeClaims.Permission, p)));
        if (user.SupervisorId is { } sid) claims.Add(new(WorktimeClaims.SupervisorId, sid.ToString()));

        var token = new JwtSecurityToken(o.Issuer, o.Audience, claims, clock.UtcNow.UtcDateTime, expires.UtcDateTime,
            new SigningCredentials(o.Key, SecurityAlgorithms.HmacSha256));
        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

public static class ClaimsPrincipalExtensions
{
    public static Guid UserId(this ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(WorktimeClaims.Subject)!);

    public static Actor ToActor(this ClaimsPrincipal p) => new(
        p.UserId(),
        Enum.Parse<Role>(p.FindFirstValue(WorktimeClaims.Role)!),
        p.FindAll(WorktimeClaims.Permission).Select(c => c.Value).ToHashSet());
}
