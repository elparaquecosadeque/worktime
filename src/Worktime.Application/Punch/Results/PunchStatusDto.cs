namespace Worktime.Application.Punch.Results;

public sealed record PunchStatusDto(DateTimeOffset? WorkingSince, IReadOnlyList<Guid> CreatedWorkLogIds);
