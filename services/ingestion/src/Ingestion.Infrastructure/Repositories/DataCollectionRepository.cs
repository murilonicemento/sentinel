using Dapper;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Repositories;
using Ingestion.Infrastructure.DbContext;

namespace Ingestion.Infrastructure.Repositories;

public class DataCollectionRepository : IDataCollectionRepository
{
    private readonly IngestionDbContext _context;

    public DataCollectionRepository(IngestionDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> RegisterAsync(DataCollection dataCollection)
    {
        var query =
            @"INSERT INTO 
                data_collection 
                    (id, data_source_id, collected_at, payload) 
                VALUES 
                    (@Id, @DataSourceId, @CollectedAt, @Payload)";

        await _context.Connection.ExecuteAsync(query, dataCollection);

        return dataCollection.Id;
    }
}