namespace Ingestion.Application.Interfaces.Events;

public interface IEventDeduplicator
{
    public Task<bool> IsDuplicateAsync(string key);
    public Task MarkAsProcessedAsync(string key, TimeSpan timeToLive);
}