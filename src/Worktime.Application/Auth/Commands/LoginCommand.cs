using Mediator;
using Worktime.Application.Auth.Results;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Permissions.Interfaces;
using Worktime.Application.Users.Interfaces;

namespace Worktime.Application.Auth.Commands;

public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResult>, IValidatable
{
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrEmpty(Password)) yield return "auth.invalid_credentials";
    }
}

public sealed class LoginHandler(
    IUserRepository users, IRolePermissionRepository matrix, IPasswordHasher hasher,
    ITokenIssuer tokens, IStampStore stamps, IUserReads reads) : ICommandHandler<LoginCommand, LoginResult>
{
    public async ValueTask<LoginResult> Handle(LoginCommand c, CancellationToken ct)
    {
        var user = await users.GetByEmailAsync(c.Email.Trim().ToLowerInvariant(), ct);
        // Same code for unknown email, wrong password and inactive user: no account enumeration.
        if (user is null || !user.IsActive || !hasher.Verify(user.PasswordHash, c.Password))
            throw new ForbiddenException("auth.invalid_credentials");

        var permissions = await matrix.GetForRoleAsync(user.Role, ct);
        await stamps.SetAsync(user.Id, user.SecurityStamp, ct);
        var token = tokens.Issue(user, permissions);
        return new LoginResult(token.Token, token.ExpiresAt, (await reads.GetAsync(user.Id, ct))!, permissions);
    }
}
