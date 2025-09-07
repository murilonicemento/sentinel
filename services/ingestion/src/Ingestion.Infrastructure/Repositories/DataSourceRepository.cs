using Dapper;
using Ingestion.Domain.AggregateRoots;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Repositories;
using Ingestion.Infrastructure.DbContext;

namespace Ingestion.Infrastructure.Repositories;

public class DataSourceRepository : IDataSourceRepository
{
    private readonly IngestionDbContext _context;

    public DataSourceRepository(IngestionDbContext context)
    {
        _context = context;
    }

    public DataSource? GetById(Guid id)
    {
        var query = @"SELECT 
                        id,
                        name,
                        data_source_type,
                        measurement_type,
                        endpoint,
                        collection_frequency
                    FROM 
                        data_source 
                    WHERE 
                        id = @Id";

        var dataSource = _context.Connection.QueryFirstOrDefault<DataSource>(query, new { Id = id });

        if (dataSource is not null)
            dataSource.DataCollections = GetDataCollectionByDataSourceId(id);

        return dataSource;
    }

    public async Task<Guid> RegisterAsync(DataSource dataSource)
    {
        var query = @"INSERT INTO
                        data_source (id, name, data_source_type, measurement_type, endpoint, collection_frequency) 
                        VALUES (@Id, @Name, @DataSourceType, @MeasurementType, @Endpoint, @CollectionFrequency)";

        await _context.Connection.ExecuteAsync(query, dataSource);

        return dataSource.Id;
    }

    private IEnumerable<DataCollection> GetDataCollectionByDataSourceId(Guid dataSourceId)
    {
        var query =
            @"SELECT id, data_source_id, collected_at, payload FROM data_collection WHERE data_source_id = @DataSourceId";

        return _context.Connection.Query<DataCollection>(query, new { DataSourceId = dataSourceId });
    }
}