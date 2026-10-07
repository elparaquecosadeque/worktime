namespace Worktime.Application.Common;

public static class RealtimeGroups
{
    public const string Admins = "admins";
    public static string User(Guid id) => $"user-{id}";
    public static string Supervisor(Guid id) => $"supervisor-{id}";
}
