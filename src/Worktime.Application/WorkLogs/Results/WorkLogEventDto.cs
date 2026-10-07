using Worktime.Domain.WorkLogs;

namespace Worktime.Application.WorkLogs.Results;

public sealed record WorkLogEventDto(DateTimeOffset At, Guid ActorId, string ActorName, WorkLogStatus? From, WorkLogStatus To, string? Reason);
