namespace AlertOrchestrator.Application.Interfaces.Messaging;

public enum IdempotencyClaimResult
{
    /// <summary>The caller owns the event until it completes or abandons it.</summary>
    Acquired,

    AlreadyProcessed,

    /// <summary>Another consumer holds an unexpired claim; the message should be retried later.</summary>
    InProgress
}

/// <summary>
/// Atomic claim on an event id, so a redelivered or concurrently consumed event is processed once. A claim that
/// is neither completed nor abandoned (process crash) expires after a short lease.
/// </summary>
public interface IIdempotencyService
{
    Task<IdempotencyClaimResult> TryBeginAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task CompleteAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task AbandonAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <summary>Raised when another consumer is processing the same event; consumers treat it as transient.</summary>
public sealed class MessageInProgressException(Guid eventId)
    : Exception($"Event {eventId} is being processed by another consumer.");
