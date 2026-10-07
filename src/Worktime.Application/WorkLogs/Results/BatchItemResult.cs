namespace Worktime.Application.WorkLogs.Results;

public sealed record BatchItemResult(Guid WorkLogId, BatchOutcome Outcome, string? Code);
