using Dapper;
using Ingestion.Domain.AggregateRoots;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Infrastructure.Write.Persistence.Postgres.DbContext;

namespace Ingestion.Infrastructure.Write.Persistence.Postgres.Repositories;

public class DataSourceRepository : IDataSourceRepository
{
    private readonly WriteDbContext _writeDbContext;

    public DataSourceRepository(WriteDbContext writeDbContext)
    {
        _writeDbContext = writeDbContext;
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

        var dataSource =
            _writeDbContext.Connection.QueryFirstOrDefault<DataSource>(query, new { Id = id, TenantId = tenantId });

        if (dataSource is not null)
            dataSource.DataCollections = GetDataCollectionByDataSourceId(id);

        return dataSource;
    }

    public async Task<(Guid dataSourceId, Guid tenantId)> RegisterAsync(DataSource dataSource)
    {
        var query = @"INSERT INTO
                        data_source (id, name, data_source_type, measurement_type, endpoint, collection_frequency, tenant_id, created_at) 
                        VALUES (@Id, @Name, @DataSourceType, @MeasurementType, @Endpoint, @CollectionFrequency, @TenantId, @CreatedAt)";

        await _writeDbContext.Connection.ExecuteAsync(query, dataSource);

        return (dataSource.Id, dataSource.TenantId);
    }

    private IEnumerable<DataCollection> GetDataCollectionByDataSourceId(Guid dataSourceId)
    {
        var query =
            @"SELECT id, data_source_id, collected_at, payload, tenant_id, created_at FROM data_collection WHERE data_source_id = @DataSourceId";

        return _writeDbContext.Connection.Query<DataCollection>(query, new { DataSourceId = dataSourceId });
    }
}