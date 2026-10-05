using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Permissions;
using Worktime.Application.Users;
using Worktime.Domain.Users;

namespace Worktime.Application.Auth;

public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResult>, IValidatable
{
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrEmpty(Password)) yield return "auth.invalid_credentials";
    }
}

public sealed record LoginResult(string Token, DateTimeOffset ExpiresAt, UserDto User, IReadOnlyList<string> Permissions);

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

public sealed record DemoAccount(Role Role, string Name, string Email, string Password, string Hint);

/// <summary>Only exists while demo seeding is enabled; the login dropdown fills credentials from it.</summary>
public interface IDemoAccounts
{
    bool Enabled { get; }
    Task<IReadOnlyList<DemoAccount>> ListAsync(CancellationToken ct);
}

public sealed record ListDemoAccountsQuery : IQuery<IReadOnlyList<DemoAccount>>;

public sealed class ListDemoAccountsHandler(IDemoAccounts demo) : IQueryHandler<ListDemoAccountsQuery, IReadOnlyList<DemoAccount>>
{
    public async ValueTask<IReadOnlyList<DemoAccount>> Handle(ListDemoAccountsQuery q, CancellationToken ct) =>
        demo.Enabled ? await demo.ListAsync(ct) : [];
}
