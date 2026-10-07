using Worktime.Domain.Common.Interfaces;

namespace Worktime.Domain.WorkLogs.Events;

public sealed record WorkLogSubmitted(Guid WorkLogId, Guid WorkerId) : IDomainEvent;
