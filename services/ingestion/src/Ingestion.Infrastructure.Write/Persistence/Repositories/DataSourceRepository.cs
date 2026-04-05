using Dapper;
using Ingestion.Domain.AggregateRoots;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Infrastructure.Write.Persistence.DbContext;

namespace Ingestion.Infrastructure.Write.Persistence.Repositories;

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
                        data_source.id AS id,
                        data_source.name AS name,
                        data_source.data_source_type AS data_source_type,
                        data_source.measurement_type AS measurement_type,
                        data_source.endpoint AS endpoint,
                        data_source.collection_frequency AS collection_frequency,
                        data_source.tenant_id AS tenant_id,
                        data_source.created_at AS created_at
                    FROM 
                        data_source 
                    INNER JOIN tenant ON tenant.id = data_source.tenant_id
                    WHERE 
                        data_source.id = @Id AND tenant_id = @TenantId AND tenant.is_active = true";

        var dataSource =
            _writeDbContext.Connection.QueryFirstOrDefault<DataSource>(query, new { Id = id, TenantId = tenantId });

        if (dataSource is null) return dataSource;

        dataSource.DataCollections = GetDataCollectionByDataSourceId(id);
        dataSource.EventPermissions = GetEventTypePermissionsByDataSourceId(id);

        return dataSource;
    }

    public async Task<DataSource?> GetByNameAndTenantAsync(string name, Guid tenantId)
    {
        var query = @"SELECT 
                        data_source.id,
                        data_source.name,
                        data_source.data_source_type,
                        data_source.measurement_type,
                        data_source.endpoint,
                        data_source.collection_frequency,
                        data_source.tenant_id,
                        data_source.created_at
                    FROM 
                        data_source 
                    INNER JOIN tenant ON tenant.id = data_source.tenant_id
                    WHERE 
                        data_source.name = @Name AND data_source.tenant_id = @TenantId AND tenant.is_active = true";

        var dataSource =
            await _writeDbContext.Connection.QueryFirstOrDefaultAsync<DataSource>(query,
                new { Name = name, TenantId = tenantId });

        if (dataSource is not null)
            dataSource.DataCollections = GetDataCollectionByDataSourceId(dataSource.Id);

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

    private IEnumerable<EventTypePermission> GetEventTypePermissionsByDataSourceId(Guid dataSourceId)
    {
        var query =
            @"SELECT id, data_source_id, event_domain, event_type FROM event_type_permission WHERE data_source_id = @DataSourceId";

        return _writeDbContext.Connection.Query<EventTypePermission>(query, new { DataSourceId = dataSourceId });
    }
}