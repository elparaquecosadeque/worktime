namespace Worktime.Application.Team.Results;

/// <param name="SupervisorId">null = workers without a supervisor (orphans).</param>
public sealed record TeamGroupDto(Guid? SupervisorId, string? SupervisorName, IReadOnlyList<TeamMemberDto> Members);
