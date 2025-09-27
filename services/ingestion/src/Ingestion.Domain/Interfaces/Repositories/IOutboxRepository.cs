using Ingestion.Domain.Outbox;

namespace Ingestion.Domain.Interfaces.Repositories;

public interface IOutboxRepository
{
    public Task<Guid> RegisterAsync(OutboxMessage outboxMessage);
    public Task<IEnumerable<OutboxRow>> GetPending();
    public Task<bool> UpdateProcessed(Guid id);
}