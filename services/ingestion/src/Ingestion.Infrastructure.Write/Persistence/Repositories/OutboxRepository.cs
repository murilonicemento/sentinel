using System.Diagnostics;
using Dapper;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.Outbox;
using Ingestion.Infrastructure.Write.Persistence.DbContext;

namespace Ingestion.Infrastructure.Write.Persistence.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly WriteDbContext _writeDbContext;

    public OutboxRepository(WriteDbContext writeDbContext)
    {
        _writeDbContext = writeDbContext;
    }

    /// <summary>
    /// Idempotent on the message id. Stores the W3C context of the current trace (the request that wrote the
    /// collection) so the relay can publish as part of that trace (ADR-0004, X-19).
    /// </summary>
    public async Task<Guid> RegisterAsync(OutboxMessage outboxMessage)
    {
        var query = @"INSERT INTO outbox 
                        (id, aggregate_id, outbox_type, payload, trace_parent)
                    VALUES (@Id, @AggregateId, @OutboxType, @Payload::jsonb, @TraceParent)
                    ON CONFLICT (id) DO NOTHING";
        await _writeDbContext.Connection.ExecuteAsync(query, new
        {
            outboxMessage.Id,
            outboxMessage.AggregateId,
            outboxMessage.OutboxType,
            outboxMessage.Payload,
            TraceParent = Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity ? activity.Id : null
        }, _writeDbContext.Transaction);
        return outboxMessage.Id;
    }

    /// <summary>
    /// Locks up to 50 pending rows; must run inside the unit of work so the lock holds until they are marked, which
    /// keeps two relay instances from publishing the same row.
    /// </summary>
    public async Task<IEnumerable<OutboxRow>> GetPendingAsync() =>
        await _writeDbContext.Connection.QueryAsync<OutboxRow>(
            "SELECT id, outbox_type, payload, trace_parent FROM outbox WHERE processed = false ORDER BY id LIMIT 50 FOR UPDATE SKIP LOCKED",
            transaction: _writeDbContext.Transaction);

    public async Task<bool> UpdateProcessedAsync(Guid id)
    {
        var affectedRows = await _writeDbContext.Connection.ExecuteAsync(
            "UPDATE outbox SET processed = true, processed_at = now() WHERE id = @Id",
            new { Id = id }, _writeDbContext.Transaction);
        return affectedRows > 0;
    }
}
