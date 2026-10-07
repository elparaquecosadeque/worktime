namespace Worktime.Application.Users.Results;

public sealed record MeDto(UserDto User, IReadOnlyList<string> Permissions, DateTimeOffset? WorkingSince, PendingRequestDto? PendingRequest);
