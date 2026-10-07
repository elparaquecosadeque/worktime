using System.Text;
using Microsoft.IdentityModel.Tokens;

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
