using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace Ingestion.Infrastructure.Read.Persistence.DbContext;

public class ReadDbContext
{
    private readonly IMongoDatabase _database;

    public ReadDbContext(IConfiguration configuration)
    {
        var client = new MongoClient(configuration.GetConnectionString("IngestionReadDatabase"));
        _database = client.GetDatabase("IngestionReadModel");
    }

    public IMongoCollection<T> GetCollection<T>(string name)
        => _database.GetCollection<T>(name);
}