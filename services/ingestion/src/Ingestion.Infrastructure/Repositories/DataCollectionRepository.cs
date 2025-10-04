using Dapper;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Infrastructure.DbContext;

namespace Ingestion.Infrastructure.Repositories;

public class DataCollectionRepository : IDataCollectionRepository
{
    private readonly IngestionDbContext _ingestionDbContext;

    public DataCollectionRepository(IngestionDbContext ingestionDbContext)
    {
        _ingestionDbContext = ingestionDbContext;
    }

    public async Task<Guid> RegisterAsync(DataCollection dataCollection)
    {
        var query =
            @"INSERT INTO 
                data_collection 
                    (id, data_source_id, collected_at, payload, tenant_id, created_at) 
                VALUES 
                    (@Id, @DataSourceId, @CollectedAt, @Payload::jsonb, @TenantId, @CreatedAt)";

        await _ingestionDbContext.Connection.ExecuteAsync(query, dataCollection);

        return dataCollection.Id;
    }
}