namespace Worktime.Application.Common.Interfaces;

/// <summary>Fast lookup of each user's current security stamp (Redis), used on every authenticated request.</summary>
public interface IStampStore
{
    Task SetAsync(Guid userId, string stamp, CancellationToken ct);
}
