namespace Ingestion.Application.Interfaces.Deduplicators;

/// <summary>
/// Atomic claim on a collection key: only one caller processes a given collection at a time, and a processed one is
/// skipped until the marker expires. A failed attempt releases the claim so the collection can be retried.
/// </summary>
public interface IEventDeduplicator
{
    /// <summary>False when the collection was already processed or is being processed by another caller.</summary>
    public Task<bool> TryBeginAsync(string key, TimeSpan lease);

    public Task MarkAsProcessedAsync(string key, TimeSpan timeToLive);

    public Task AbandonAsync(string key);
}
