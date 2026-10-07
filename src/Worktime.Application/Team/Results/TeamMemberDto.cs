namespace Worktime.Application.Team.Results;

public sealed record TeamMemberDto(
    Guid WorkerId, string Name, string TimeZoneId, PresenceState Presence, DateTimeOffset? WorkingSince,
    double MonthHoursApproved, double MonthHoursPending, int PendingCount, int NeedsRevisionCount);
