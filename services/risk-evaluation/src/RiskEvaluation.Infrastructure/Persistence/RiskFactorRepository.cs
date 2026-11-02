using MongoDB.Driver;
using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Enums;
using RiskEvaluation.Domain.Repositories;

namespace RiskEvaluation.Infrastructure.Persistence;

public class RiskFactorRepository : IRiskFactorRepository
{
    private readonly IMongoCollection<RiskFactor> _collection;

    public RiskFactorRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<RiskFactor>("RiskFactors");
    }

    public async Task<RiskFactor?> GetByTypeAsync(RiskFactorType type, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RiskFactor>.Filter.Eq(x => x.Type, type);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RiskFactor>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(_ => true).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RiskFactor>> GetByTypesAsync(IEnumerable<RiskFactorType> types, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RiskFactor>.Filter.In(x => x.Type, types);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(RiskFactor riskFactor, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RiskFactor>.Filter.Eq(x => x.Type, riskFactor.Type);
        var options = new ReplaceOptions { IsUpsert = true };
        await _collection.ReplaceOneAsync(filter, riskFactor, options, cancellationToken);
    }

    public async Task SaveManyAsync(IEnumerable<RiskFactor> riskFactors, CancellationToken cancellationToken = default)
    {
        var models = new List<WriteModel<RiskFactor>>();
        
        foreach (var factor in riskFactors)
        {
            var filter = Builders<RiskFactor>.Filter.Eq(x => x.Type, factor.Type);
            models.Add(new ReplaceOneModel<RiskFactor>(filter, factor) { IsUpsert = true });
        }

        if (models.Count > 0)
        {
            await _collection.BulkWriteAsync(models, cancellationToken: cancellationToken);
        }
    }

    public async Task DeleteByTypeAsync(RiskFactorType type, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RiskFactor>.Filter.Eq(x => x.Type, type);
        await _collection.DeleteOneAsync(filter, cancellationToken);
    }
}
