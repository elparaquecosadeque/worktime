using Mediator;
using Worktime.Application.Auth.Interfaces;
using Worktime.Application.Auth.Results;

namespace Worktime.Application.Auth.Queries;

public sealed record ListDemoAccountsQuery : IQuery<IReadOnlyList<DemoAccount>>;

public sealed class ListDemoAccountsHandler(IDemoAccounts demo) : IQueryHandler<ListDemoAccountsQuery, IReadOnlyList<DemoAccount>>
{
    public async ValueTask<IReadOnlyList<DemoAccount>> Handle(ListDemoAccountsQuery q, CancellationToken ct) =>
        demo.Enabled ? await demo.ListAsync(ct) : [];
}
