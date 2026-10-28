using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Application.Interfaces.Messaging;
using RiskEvaluation.Application.Interfaces.Services;
using RiskEvaluation.Domain.Enums;
using RiskEvaluation.Domain.Events;
using RiskEvaluation.Domain.Interfaces;
using RiskEvaluation.Domain.Repositories;
using RiskEvaluation.Domain.Services;

namespace RiskEvaluation.Application.Services;

public class RiskEvaluationService : IRiskEvaluationService
{
    private readonly IRiskEvaluationRepository _repository;
    private readonly IEventPublisher _eventPublisher;
    private readonly RiskCalculationService _calculationService;
    private readonly IRecentScoresCache _cache;
    private readonly ICatalogSnapshotRepository _catalogSnapshots;
    private readonly ILogger<RiskEvaluationService> _logger;

    public RiskEvaluationService(
        IRiskEvaluationRepository repository,
        IEventPublisher eventPublisher,
        RiskCalculationService calculationService,
        IRecentScoresCache cache,
        ICatalogSnapshotRepository catalogSnapshots,
        ILogger<RiskEvaluationService> logger)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
        _calculationService = calculationService;
        _cache = cache;
        _catalogSnapshots = catalogSnapshots;
        _logger = logger;
    }

    public async Task<RiskEvaluationEntity> EvaluateRiskAsync(
        int latitude,
        int longitude,
        RiskMetrics metrics,
        RiskEvents events,
        EvaluationSource? source = null)
    {
        // The latest published catalog: regional factors and the matrix floor. Without one, the score is the base one.
        var catalog = await _catalogSnapshots.GetLatestAsync();
        var matrixLevel = catalog?.MatrixLevelFor(source?.EventType, source?.Intensity);

        var cached = await _cache.GetAsync(latitude, longitude);
        
        if (cached != null && IsRecent(cached.CalculatedAt))
        {
            var cachedLevel = Floor(Enum.Parse<RiskLevel>(cached.Level), matrixLevel);
            var cachedEvaluation = new RiskEvaluationEntity(latitude, longitude, cached.Score, cachedLevel);
            await _repository.AddAsync(cachedEvaluation);
            return cachedEvaluation;
        }

        var (metricWeights, eventWeights) = await _calculationService.LoadWeightsAsync();
        // No service provides the geographic context yet (RE-14): the calculation runs without it.
        GeospatialContext? geoContext = null;

        var baseScore = _calculationService.CalculateRiskScore(metrics, events, metricWeights, eventWeights, geoContext);
        var regionalFactor = catalog?.RegionalFactorAt(source?.Latitude ?? latitude, source?.Longitude ?? longitude)
                             ?? 1.0;
        var score = Math.Clamp(baseScore * regionalFactor, 0.0, 1.0);
        var scoreLevel = RiskCalculationService.ClassifyRiskLevel(score);
        var level = Floor(scoreLevel, matrixLevel);

        await _cache.SetAsync(latitude, longitude, new RiskScoreCacheEntry
        {
            Score = score,
            // The cache keeps the score's own level; the matrix floor depends on each event and is applied on read.
            Level = scoreLevel.ToString(),
            CalculatedAt = DateTime.UtcNow,
            Metrics = metrics,
            Events = events
        });

        var evaluation = new RiskEvaluationEntity(latitude, longitude, score, level);
        await _repository.AddAsync(evaluation);

        var riskEvaluatedEvent = new RiskEvaluatedEvent(
            evaluation.Id,
            evaluation.Latitude,
            evaluation.Longitude,
            evaluation.Score,
            evaluation.Level.ToString(),
            evaluation.Timestamp);

        await _eventPublisher.PublishAsync(riskEvaluatedEvent);

        if (level != RiskLevel.High && level != RiskLevel.Critical) return evaluation;

        var highRiskEvent = new HighRiskDetectedEvent(
            evaluation.Id,
            evaluation.Latitude,
            evaluation.Longitude,
            evaluation.Score,
            evaluation.Level.ToString(),
            evaluation.Timestamp);

        await _eventPublisher.PublishAsync(highRiskEvent);

        return evaluation;
    }

    // The catalog matrix can raise the level for an event type and intensity, never lower it.
    private static RiskLevel Floor(RiskLevel level, RiskLevel? matrixLevel) =>
        matrixLevel is { } floor && floor > level ? floor : level;

    private static bool IsRecent(DateTime calculatedAt) => DateTime.UtcNow - calculatedAt < TimeSpan.FromMinutes(5);
}