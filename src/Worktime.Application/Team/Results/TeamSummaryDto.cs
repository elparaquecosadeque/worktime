namespace Worktime.Application.Team.Results;

public sealed record TeamSummaryDto(DateTimeOffset GeneratedAt, IReadOnlyList<TeamGroupDto> Groups);
