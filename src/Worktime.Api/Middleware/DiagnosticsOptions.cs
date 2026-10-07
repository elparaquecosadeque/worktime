namespace Worktime.Api.Middleware;

public sealed class DiagnosticsOptions
{
    public const string Section = "Diagnostics";
    public int SlowRequestMs { get; set; } = 500;
}
