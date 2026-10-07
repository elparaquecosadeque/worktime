namespace Worktime.Application.Team.Results;

public sealed record TeamWorkerRow(Guid Id, string Name, string TimeZoneId, Guid? SupervisorId, DateTimeOffset? WorkingSince);
