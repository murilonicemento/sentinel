using Dapper;
using Ingestion.Domain.AggregateRoots;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.Repositories;
using Ingestion.Infrastructure.DbContext;

namespace Ingestion.Infrastructure.Repositories;

public class DataSourceRepository : IDataSourceRepository
{
    private readonly IngestionDbContext _ingestionDbContext;

    public DataSourceRepository(IngestionDbContext ingestionDbContext)
    {
        _ingestionDbContext = ingestionDbContext;
    }

    public DataSource? GetByIdAndTenantId(Guid id, Guid tenantId)
    {
        var query = @"SELECT 
                        id,
                        name,
                        data_source_type,
                        measurement_type,
                        endpoint,
                        collection_frequency,
                        tenant_id,
                        created_at
                    FROM 
                        data_source 
                    WHERE 
                        id = @Id AND tenant_id = @TenantId";

        var dataSource = _ingestionDbContext.Connection.QueryFirstOrDefault<DataSource>(query, new { Id = id, TenantId = tenantId });

        if (dataSource is not null)
            dataSource.DataCollections = GetDataCollectionByDataSourceId(id);

        return dataSource;
    }

    public async Task<Guid> RegisterAsync(DataSource dataSource)
    {
        var query = @"INSERT INTO
                        data_source (id, name, data_source_type, measurement_type, endpoint, collection_frequency, tenant_id, created_at) 
                        VALUES (@Id, @Name, @DataSourceType, @MeasurementType, @Endpoint, @CollectionFrequency, @TenantId, @CreatedAt)";

        await _ingestionDbContext.Connection.ExecuteAsync(query, dataSource);

        return dataSource.Id;
    }

    private IEnumerable<DataCollection> GetDataCollectionByDataSourceId(Guid dataSourceId)
    {
        var query =
            @"SELECT id, data_source_id, collected_at, payload, tenant_id, created_at FROM data_collection WHERE data_source_id = @DataSourceId";

        return _ingestionDbContext.Connection.Query<DataCollection>(query, new { DataSourceId = dataSourceId });
    }
}