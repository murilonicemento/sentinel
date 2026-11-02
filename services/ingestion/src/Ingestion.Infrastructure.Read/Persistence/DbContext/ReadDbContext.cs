using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ingestion.Infrastructure.Read.Persistence.DbContext;

public class ReadDbContext
{
    private readonly IMongoDatabase _database;

    public ReadDbContext(IConfiguration configuration)
    {
        var client = new MongoClient(configuration.GetConnectionString("IngestionReadDatabase"));
        _database = client.GetDatabase("IngestionReadDatabase");
    }

    public virtual IMongoCollection<T> GetCollection<T>(string name)
        => _database.GetCollection<T>(name);

    public virtual Task PingAsync(CancellationToken cancellationToken) =>
        _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
}