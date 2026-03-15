using Ingestion.Domain.Outbox;

namespace Ingestion.Domain.Interfaces.Repositories;

public interface IOutboxRepository
{
    public Task<Guid> RegisterAsync(OutboxMessage outboxMessage);
    public Task<IEnumerable<OutboxRow>> GetPendingAsync();
    public Task<bool> UpdateProcessedAsync(Guid id);
}