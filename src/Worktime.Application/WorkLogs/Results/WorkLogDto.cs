using Worktime.Domain.WorkLogs;

namespace Worktime.Application.WorkLogs.Results;

public sealed record WorkLogDto(
    Guid Id, Guid WorkerId, string WorkerName, string WorkerTimeZoneId,
    DateTimeOffset StartAt, DateTimeOffset EndAt, WorkLogSource Source, string? Note, WorkLogStatus Status,
    IReadOnlyList<WorkLogEventDto> History);
