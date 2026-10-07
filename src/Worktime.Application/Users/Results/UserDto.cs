using Worktime.Domain.Users;

namespace Worktime.Application.Users.Results;

public sealed record UserDto(Guid Id, string Name, string Email, Role Role, bool IsActive, Guid? SupervisorId, string? SupervisorName, string TimeZoneId);
