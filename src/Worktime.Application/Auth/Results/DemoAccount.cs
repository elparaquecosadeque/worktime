using Worktime.Domain.Users;

namespace Worktime.Application.Auth.Results;

public sealed record DemoAccount(Role Role, string Name, string Email, string Password, string Hint);
