using Dapper;
using Ingestion.Domain.AggregateRoots;
using Ingestion.Domain.Repositories;
using Ingestion.Infrastructure.DbContext;

namespace Ingestion.Infrastructure.Repositories;

public class DataSourceRepository : IDataSourceRepository
{
    private readonly IngestionDbContext _context;

    public DataSourceRepository(IngestionDbContext dbContext)
    {
        _context = dbContext;
    }

    public async Task<Guid> Register(DataSource dataSource)
    {
        const string query =
            "INSERT INTO data_source (id, name, type, endpoint, collection_frequency) VALUES (@Id, @Name, @Type, @Endpoint, @CollectionFrequency)";

        await _context.Connection.ExecuteAsync(query, dataSource);

        return dataSource.Id;
    }
}