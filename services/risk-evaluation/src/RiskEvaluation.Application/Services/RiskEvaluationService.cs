using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Domain.Services;
using RiskEvaluation.Domain.Events;
using RiskEvaluation.Domain.Enums;

namespace RiskEvaluation.Application.Services;

public class RiskEvaluationService : IRiskEvaluationService
{
    private readonly IRiskEvaluationRepository _repository;
    private readonly IEventPublisher _eventPublisher;
    private readonly RiskCalculationService _calculationService;

    public RiskEvaluationService(
        IRiskEvaluationRepository repository,
        IEventPublisher eventPublisher,
        RiskCalculationService calculationService)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
        _calculationService = calculationService;
    }

    public async Task<RiskEvaluationEntity> EvaluateRiskAsync(string location, double gust, double precipitation, double pressure)
    {
        double score = _calculationService.CalculateRiskScore(gust, precipitation, pressure);
        var level = _calculationService.ClassifyRiskLevel(score);

        var evaluation = new RiskEvaluationEntity(location, score, level);

        await _repository.AddAsync(evaluation);

        // Publish events
        var riskEvaluatedEvent = new RiskEvaluatedEvent(
            evaluation.Id,
            evaluation.Location,
            evaluation.Score,
            evaluation.Level.ToString(),
            evaluation.Timestamp);

        await _eventPublisher.PublishAsync(riskEvaluatedEvent);

        if (level == Domain.Enums.RiskLevel.High || level == Domain.Enums.RiskLevel.Critical)
        {
            var highRiskEvent = new HighRiskDetectedEvent(
                evaluation.Id,
                evaluation.Location,
                evaluation.Score,
                evaluation.Level.ToString(),
                evaluation.Timestamp);

            await _eventPublisher.PublishAsync(highRiskEvent);
        }

        return evaluation;
    }
}