namespace Worktime.Application.Presence.Results;

public sealed record PresenceChangedPayload(Guid WorkerId, bool Online);
