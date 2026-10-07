using Worktime.Application.Users.Results;

namespace Worktime.Application.Auth.Results;

public sealed record LoginResult(string Token, DateTimeOffset ExpiresAt, UserDto User, IReadOnlyList<string> Permissions);
