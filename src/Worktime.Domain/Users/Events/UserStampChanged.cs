using Worktime.Domain.Common.Interfaces;

namespace Worktime.Domain.Users.Events;

/// <summary>Any stamp change must reach the stamp store; <see cref="Reason"/> non-null also kicks live sessions.</summary>
public sealed record UserStampChanged(Guid UserId, string Stamp, string? Reason) : IDomainEvent;
