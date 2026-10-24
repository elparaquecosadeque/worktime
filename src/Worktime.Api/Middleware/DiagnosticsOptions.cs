namespace Worktime.Api.Middleware;

public sealed class DiagnosticsOptions
{
    public const string Section = "Diagnostics";
    public int SlowRequestMs { get; set; } = 500;

    /// <summary>/health also reports the reads adapter (EF or Dapper). Local/dev only: the server never turns it on.</summary>
    public bool ExposeReadsAdapter { get; set; }
}
