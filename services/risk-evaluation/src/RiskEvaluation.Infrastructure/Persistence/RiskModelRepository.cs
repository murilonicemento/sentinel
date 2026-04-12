using MongoDB.Driver;
using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Repositories;

namespace RiskEvaluation.Infrastructure.Persistence;

public class RiskModelRepository : IRiskModelRepository
{
    private readonly IMongoCollection<RiskModel> _collection;

    public RiskModelRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<RiskModel>("RiskModels");
    }

    public async Task<RiskModel?> GetByVersionAsync(string version, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RiskModel>.Filter.Eq(x => x.Version, version);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RiskModel?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(_ => true)
            .SortByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveAsync(RiskModel riskModel, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RiskModel>.Filter.Eq(x => x.Version, riskModel.Version);
        var options = new ReplaceOptions { IsUpsert = true };
        await _collection.ReplaceOneAsync(filter, riskModel, options, cancellationToken);
    }

    public async Task<IReadOnlyList<RiskModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(_ => true).ToListAsync(cancellationToken);
    }
}
