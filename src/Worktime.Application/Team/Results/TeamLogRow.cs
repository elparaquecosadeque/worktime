using Worktime.Domain.WorkLogs;

namespace Worktime.Application.Team.Results;

public sealed record TeamLogRow(Guid WorkerId, DateTimeOffset StartAt, DateTimeOffset EndAt, WorkLogStatus Status);
