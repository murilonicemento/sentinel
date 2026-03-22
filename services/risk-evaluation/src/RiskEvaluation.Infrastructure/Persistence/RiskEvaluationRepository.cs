using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Domain.Entities;
using MongoDB.Driver;

namespace RiskEvaluation.Infrastructure.Persistence;

public class RiskEvaluationRepository : IRiskEvaluationRepository
{
    private readonly IMongoCollection<RiskEvaluationEntity> _collection;

    public RiskEvaluationRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<RiskEvaluationEntity>("RiskEvaluations");
    }

    public async Task AddAsync(RiskEvaluationEntity evaluation)
    {
        await _collection.InsertOneAsync(evaluation);
    }

    public async Task<RiskEvaluationEntity?> GetByLocationAsync(string location)
    {
        return await _collection.Find(e => e.Location == location).FirstOrDefaultAsync();
    }

    public async Task UpdateAsync(RiskEvaluationEntity evaluation)
    {
        await _collection.ReplaceOneAsync(e => e.Id == evaluation.Id, evaluation);
    }
}