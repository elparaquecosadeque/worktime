using Worktime.Domain.Common.Interfaces;

namespace Worktime.Domain.WorkLogs.Events;

public sealed record WorkLogEdited(Guid WorkLogId, Guid WorkerId) : IDomainEvent;
