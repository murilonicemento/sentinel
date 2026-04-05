namespace Ingestion.Application.Interfaces.Deduplicators;

public interface IEventDeduplicator
{
    public Task<bool> IsDuplicateAsync(string key);
    public Task MarkAsProcessedAsync(string key, TimeSpan timeToLive);
}