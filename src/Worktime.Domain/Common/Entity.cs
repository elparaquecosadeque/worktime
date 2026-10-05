namespace Worktime.Domain.Common;

public interface IDomainEvent;

public abstract class Entity
{
    private readonly List<IDomainEvent> _events = [];

    public Guid Id { get; protected init; } = Guid.NewGuid();

    public IReadOnlyList<IDomainEvent> DomainEvents => _events;

    protected void Raise(IDomainEvent e) => _events.Add(e);

    public void ClearDomainEvents() => _events.Clear();
}
