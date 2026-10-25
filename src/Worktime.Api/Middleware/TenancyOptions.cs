namespace Worktime.Api.Middleware;

/// <summary>
/// Hosts: <see cref="RootHost"/> serves the space picker; <c>{slug}{TenantHostSuffix}</c> serves one tenant.
/// Server: worktime.gerardoleon.dev / -worktime.gerardoleon.dev. Local: localhost / .localhost (browsers resolve *.localhost).
/// </summary>
public sealed class TenancyOptions
{
    public const string Section = "Tenancy";
    public string RootHost { get; set; } = "localhost";
    public string TenantHostSuffix { get; set; } = ".localhost";

    /// <summary>The slug a host names, or null for the root host and anything else.</summary>
    public string? SlugOf(string host)
    {
        host = host.ToLowerInvariant();
        return host.Length > TenantHostSuffix.Length && host.EndsWith(TenantHostSuffix, StringComparison.Ordinal)
            ? host[..^TenantHostSuffix.Length]
            : null;
    }

    /// <summary>Without port: the browser keeps its own when it swaps the host (nginx forwards $host, port-less).</summary>
    public string HostOf(string slug) => slug + TenantHostSuffix;
}
