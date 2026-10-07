namespace Worktime.Application.Assignments.Results;

public sealed record AssignmentRequestDto(
    Guid Id, Guid WorkerId, string WorkerName, string? Note,
    Guid? PreferredSupervisorId, string? PreferredSupervisorName, DateTimeOffset CreatedAt);
