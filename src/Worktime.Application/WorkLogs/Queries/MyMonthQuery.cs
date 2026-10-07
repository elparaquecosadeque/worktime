using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Users.Interfaces;
using Worktime.Application.WorkLogs.Interfaces;
using Worktime.Application.WorkLogs.Results;

namespace Worktime.Application.WorkLogs.Queries;

/// <summary>The worker's month, bounded in the worker's own time zone.</summary>
public sealed record MyMonthQuery(Actor Actor, int Year, int Month) : IQuery<IReadOnlyList<WorkLogDto>>, IValidatable
{
    public IEnumerable<string> Validate()
    {
        if (Month is < 1 or > 12 || Year is < 2000 or > 2100) yield return "worklog.invalid_month";
    }
}

public sealed class MyMonthHandler(IUserRepository users, IWorkLogReads reads) : IQueryHandler<MyMonthQuery, IReadOnlyList<WorkLogDto>>
{
    public async ValueTask<IReadOnlyList<WorkLogDto>> Handle(MyMonthQuery q, CancellationToken ct)
    {
        var worker = await users.GetAsync(q.Actor.Id, ct) ?? throw new NotFoundException("user.not_found");
        var (from, to) = MonthBounds(q.Year, q.Month, worker.TimeZone);
        return await reads.ListForWorkerAsync(worker.Id, from, to, ct);
    }

    public static (DateTimeOffset From, DateTimeOffset To) MonthBounds(int year, int month, TimeZoneInfo zone)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);
        // timestamptz parameters must be UTC (offset 0) for Npgsql.
        return (new DateTimeOffset(start, zone.GetUtcOffset(start)).ToUniversalTime(),
                new DateTimeOffset(end, zone.GetUtcOffset(end)).ToUniversalTime());
    }
}
