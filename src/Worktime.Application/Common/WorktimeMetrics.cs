using System.Diagnostics.Metrics;

namespace Worktime.Application.Common;

/// <summary>
/// Native System.Diagnostics.Metrics instruments. Inspect with
/// <c>dotnet-counters monitor -n Worktime.Api --counters Worktime</c>; an OpenTelemetry exporter can be added later unchanged.
/// </summary>
public sealed class WorktimeMetrics
{
    public const string MeterName = "Worktime";

    private readonly Counter<long> _decisions;
    private readonly Counter<long> _conflicts;
    private readonly UpDownCounter<long> _connections;
    private readonly Histogram<double> _requestDuration;

    public WorktimeMetrics(IMeterFactory factory)
    {
        var meter = factory.Create(MeterName);
        _decisions = meter.CreateCounter<long>("worklogs.decisions", description: "Work log decisions committed");
        _conflicts = meter.CreateCounter<long>("worklogs.concurrency_conflicts", description: "Decisions that lost a race");
        _connections = meter.CreateUpDownCounter<long>("signalr.connections", description: "Live hub connections on this instance");
        _requestDuration = meter.CreateHistogram<double>("http.server.request.duration_ms", unit: "ms");
    }

    public void Decision(string decision) => _decisions.Add(1, new KeyValuePair<string, object?>("decision", decision));
    public void Conflict(string code) => _conflicts.Add(1, new KeyValuePair<string, object?>("code", code));
    public void ConnectionOpened() => _connections.Add(1);
    public void ConnectionClosed() => _connections.Add(-1);

    public void Request(string route, int status, double ms) =>
        _requestDuration.Record(ms, new KeyValuePair<string, object?>("route", route), new KeyValuePair<string, object?>("status", status));
}
