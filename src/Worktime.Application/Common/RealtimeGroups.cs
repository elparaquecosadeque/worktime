namespace Worktime.Application.Common;

/// <summary>User and supervisor groups are keyed by globally unique ids; the tenant-wide ones carry the tenant.</summary>
public static class RealtimeGroups
{
    public static string Admins(Guid tenantId) => $"admins-{tenantId}";
    public static string Tenant(Guid tenantId) => $"tenant-{tenantId}";
    public static string User(Guid id) => $"user-{id}";
    public static string Supervisor(Guid id) => $"supervisor-{id}";
}
