using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Repositories;

namespace RiskEvaluation.Infrastructure.Persistence;

public class CatalogVersionRepository : ICatalogVersionRepository
{
    private readonly IMongoCollection<CatalogVersion> _collection;

    static CatalogVersionRepository()
    {
        // The domain entity has no Mongo id; the server-generated _id is ignored when reading.
        BsonClassMap.TryRegisterClassMap<CatalogVersion>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);
        });
    }

    public CatalogVersionRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<CatalogVersion>("CatalogVersions");
    }

    public Task SaveAsync(CatalogVersion catalogVersion, CancellationToken cancellationToken = default) =>
        _collection.ReplaceOneAsync(Builders<CatalogVersion>.Filter.Eq(x => x.Version, catalogVersion.Version),
            catalogVersion, new ReplaceOptions { IsUpsert = true }, cancellationToken);

    public async Task<CatalogVersion?> GetLatestAsync(CancellationToken cancellationToken = default) =>
        await _collection.Find(_ => true).SortByDescending(x => x.Version).FirstOrDefaultAsync(cancellationToken);
}
