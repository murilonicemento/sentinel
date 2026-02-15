using Dapper;
using Geospatial.Domain.Repositories;
using Geospatial.Infrastructure.Persistence.DbContext;

namespace Geospatial.Infrastructure.Persistence.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly ApplicationDbContext _applicationDbContext;

    public OutboxRepository(ApplicationDbContext applicationDbContext)
    {
        _applicationDbContext = applicationDbContext;
    }

    public async Task<bool> ExistsPending(Guid aggregateId) =>
        await _applicationDbContext.Connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM outbox WHERE aggregate_id = @AggregateId AND processed = false)",
            new { AggregateId = aggregateId });

    public async Task<bool> UpdateProcessed(Guid id)
    {
        var affectedRows = await _applicationDbContext.Connection.ExecuteAsync(
            "UPDATE outbox SET processed = true, processed_at = now() WHERE aggregate_id  = @AggregateId",
            new { AggregateId = id });

        return affectedRows > 0;
    }
}