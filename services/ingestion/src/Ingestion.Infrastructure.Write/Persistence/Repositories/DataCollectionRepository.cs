using Dapper;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Infrastructure.Write.Persistence.Postgres.DbContext;

namespace Ingestion.Infrastructure.Write.Persistence.Postgres.Repositories;

public class DataCollectionRepository : IDataCollectionRepository
{
    private readonly WriteDbContext _writeDbContext;

    public DataCollectionRepository(WriteDbContext writeDbContext)
    {
        _writeDbContext = writeDbContext;
    }

    public async Task<Guid> RegisterAsync(DataCollection dataCollection)
    {
        var query =
            @"INSERT INTO 
                data_collection 
                    (id, data_source_id, collected_at, payload, tenant_id, created_at) 
                VALUES 
                    (@Id, @DataSourceId, @CollectedAt, @Payload::jsonb, @TenantId, @CreatedAt)";

        await _writeDbContext.Connection.ExecuteAsync(query, dataCollection);

        return dataCollection.Id;
    }
}