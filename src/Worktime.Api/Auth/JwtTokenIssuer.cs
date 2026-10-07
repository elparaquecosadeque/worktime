using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Domain.Users;

namespace Worktime.Api.Auth;

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
