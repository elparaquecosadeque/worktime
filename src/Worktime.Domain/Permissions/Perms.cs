namespace Worktime.Domain.Permissions;

/// <summary>Fixed catalog: endpoints reference these, the admin only toggles who holds them.</summary>
public static class Perms
{
    public const string PunchSelf = "punch:self";
    public const string WorkLogsSubmit = "worklogs:submit";
    public const string WorkLogsApprove = "worklogs:approve";
    public const string AssignmentsRequest = "assignments:request";
    public const string AssignmentsResolve = "assignments:resolve";
    public const string WorkersManage = "workers:manage";
    public const string UsersManage = "users:manage";
    public const string TeamView = "team:view";
    public const string MonitorAll = "monitor:all";
    public const string PermissionsManage = "permissions:manage";

    public static readonly IReadOnlyList<string> All =
    [
        PunchSelf, WorkLogsSubmit, WorkLogsApprove, AssignmentsRequest, AssignmentsResolve,
        WorkersManage, UsersManage, TeamView, MonitorAll, PermissionsManage,
    ];
}
