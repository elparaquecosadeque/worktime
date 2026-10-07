namespace Worktime.Application.Common;

public sealed record IssuedToken(string Token, DateTimeOffset ExpiresAt);
