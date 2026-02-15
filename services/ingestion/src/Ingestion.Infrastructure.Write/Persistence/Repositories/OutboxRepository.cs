using Dapper;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.Outbox;
using Ingestion.Infrastructure.Write.Persistence.DbContext;

namespace Ingestion.Infrastructure.Write.Persistence.Postgres.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly WriteDbContext _writeDbContext;

    public OutboxRepository(WriteDbContext writeDbContext)
    {
        _writeDbContext = writeDbContext;
    }

    public async Task<Guid> RegisterAsync(OutboxMessage outboxMessage)
    {
        var query = @"INSERT INTO outbox 
                        (id, aggregate_id, outbox_type, payload)
                    VALUES (@Id, @AggregateId, @OutboxType, @Payload::jsonb)";

        await _writeDbContext.Connection.ExecuteAsync(query, outboxMessage);

        return outboxMessage.Id;
    }

    public async Task<IEnumerable<OutboxRow>> GetPending() =>
        await _writeDbContext.Connection.QueryAsync<OutboxRow>(
            "SELECT id, outbox_type, payload FROM outbox WHERE processed = false LIMIT 50 FOR UPDATE SKIP LOCKED");
}