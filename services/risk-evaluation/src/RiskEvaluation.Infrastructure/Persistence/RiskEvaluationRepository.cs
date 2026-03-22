using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Domain.Entities;
using MongoDB.Driver;
using RiskEvaluation.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace RiskEvaluation.Infrastructure.Persistence;

public class RiskEvaluationRepository : IRiskEvaluationRepository
{
    private readonly IMongoCollection<RiskEvaluationEntity> _collection;
    private readonly ILogger<RiskEvaluationRepository> _logger;

    public RiskEvaluationRepository(IMongoDatabase database, ILogger<RiskEvaluationRepository> logger)
    {
        _collection = database.GetCollection<RiskEvaluationEntity>("RiskEvaluations");
        _logger = logger;
    }

    public async Task AddAsync(RiskEvaluationEntity evaluation)
    {
        _logger.LogInformation("Inserting risk evaluation to MongoDB. ID: {Id}, Location: {Location}", evaluation.Id,
            evaluation.Location);
        await _collection.InsertOneAsync(evaluation);
        _logger.LogInformation("Risk evaluation inserted successfully. ID: {Id}", evaluation.Id);
    }

    public async Task<RiskEvaluationEntity?> GetByIdAsync(Guid id)
    {
        _logger.LogInformation("Querying risk evaluation by ID: {Id}", id);
        var result = await _collection.Find(e => e.Id == id).FirstOrDefaultAsync();
        if (result == null)
            _logger.LogWarning("Risk evaluation not found in MongoDB. ID: {Id}", id);
        else
            _logger.LogInformation("Risk evaluation found in MongoDB. ID: {Id}, Location: {Location}", id,
                result.Location);
        return result;
    }

    public async Task<List<RiskEvaluationEntity>> GetByLocationAsync(string location)
    {
        _logger.LogInformation("Querying risk evaluations by location: {Location}", location);
        var results = await _collection.Find(e => e.Location == location).ToListAsync();
        _logger.LogInformation("Found {Count} risk evaluations for location: {Location}", results.Count, location);
        return results;
    }

    public async Task UpdateAsync(RiskEvaluationEntity evaluation)
    {
        _logger.LogInformation("Updating risk evaluation in MongoDB. ID: {Id}, Location: {Location}", evaluation.Id,
            evaluation.Location);
        var result = await _collection.ReplaceOneAsync(e => e.Id == evaluation.Id, evaluation);
        if (result.ModifiedCount == 0)
            _logger.LogWarning("No risk evaluation was updated. ID: {Id}", evaluation.Id);
        else
            _logger.LogInformation("Risk evaluation updated successfully. ID: {Id}", evaluation.Id);
    }
}