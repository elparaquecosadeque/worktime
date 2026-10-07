namespace Worktime.Application.Users.Results;

public sealed record PendingRequestDto(Guid Id, DateTimeOffset CreatedAt, string? Note, Guid? PreferredSupervisorId);
