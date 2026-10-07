namespace Worktime.Infrastructure.Redis;

internal static class Keys
{
    public const string Stamps = "worktime:stamps";          // hash userId -> stamp
    public const string PresenceSeen = "worktime:presence:seen"; // zset userId -> last heartbeat (unix ms)
    public const string PresenceConns = "worktime:presence:conns"; // hash userId -> live connection count
    public static string Cache(string key) => $"worktime:cache:{key}";
}
