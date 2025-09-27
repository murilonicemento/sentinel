using Dapper;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.Outbox;
using Ingestion.Domain.Repositories;
using Ingestion.Infrastructure.DbContext;

namespace Ingestion.Infrastructure.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly IngestionDbContext _ingestionDbContext;

    public OutboxRepository(IngestionDbContext ingestionDbContext)
    {
        _ingestionDbContext = ingestionDbContext;
    }

    public async Task<Guid> RegisterAsync(OutboxMessage outboxMessage)
    {
        var query = @"INSERT INTO outbox 
                        (id, aggregate_id, outbox_type, payload)
                    VALUES (@Id, @AggregateId, @OutboxType, @Payload)";

        await _ingestionDbContext.Connection.ExecuteAsync(query, outboxMessage);

        return outboxMessage.Id;
    }

    public async Task<IEnumerable<OutboxRow>> GetPending() =>
        await _ingestionDbContext.Connection.QueryAsync<OutboxRow>(
            "SELECT id, outbox_type, payload FROM outbox WHERE processed = false LIMIT 50 FOR UPDATE SKIP LOCKED");

    public async Task<bool> UpdateProcessed(Guid id)
    {
        var affectedRows = await _ingestionDbContext.Connection.ExecuteAsync(
            "UPDATE outbox SET processed = true, processed_at = now() WHERE id = @Id", new { id });

        return affectedRows > 0;
    }
}