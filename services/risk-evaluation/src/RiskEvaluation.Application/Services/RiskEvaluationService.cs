using System.Globalization;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.Contracts;
using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Application.Interfaces.Messaging;
using RiskEvaluation.Application.Interfaces.Services;
using RiskEvaluation.Domain.Entities;
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
    private readonly IRiskUpdatedPublisher _riskUpdatedPublisher;
    private readonly RiskCalculationService _calculationService;
    private readonly IRecentScoresCache _cache;
    private readonly ICatalogSnapshotRepository _catalogSnapshots;
    private readonly ILogger<RiskEvaluationService> _logger;

    public RiskEvaluationService(
        IRiskEvaluationRepository repository,
        IEventPublisher eventPublisher,
        IRiskUpdatedPublisher riskUpdatedPublisher,
        RiskCalculationService calculationService,
        IRecentScoresCache cache,
        ICatalogSnapshotRepository catalogSnapshots,
        ILogger<RiskEvaluationService> logger)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
        _riskUpdatedPublisher = riskUpdatedPublisher;
        _calculationService = calculationService;
        _cache = cache;
        _catalogSnapshots = catalogSnapshots;
        _logger = logger;
    }

    public async Task<RiskEvaluationEntity> EvaluateRiskAsync(
        double latitude,
        double longitude,
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
            await PublishRiskUpdatedAsync(cachedEvaluation, catalog, source);
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
        await PublishRiskUpdatedAsync(evaluation, catalog, source);

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

    /// <summary>
    /// Hands the evaluation to alert-orchestrator. An alert window belongs to a tenant and a catalog region, so an
    /// evaluation without the sensor event's id and tenant, or outside every region of the catalog copy, raises none.
    /// </summary>
    private async Task PublishRiskUpdatedAsync(RiskEvaluationEntity evaluation, RiskCatalogSnapshot? catalog,
        EvaluationSource? source)
    {
        if (source is not { SourceEventId: { } eventId, EventType: { Length: > 0 } eventType } ||
            string.IsNullOrWhiteSpace(source.TenantId)) return;

        var region = catalog?.RegionAt(source.Latitude, source.Longitude);
        if (region is null)
        {
            _logger.LogInformation(
                "Sensor event {EventId} is outside every region of the catalog copy; no risk-updated published",
                eventId);
            return;
        }

        var metadata = new Dictionary<string, string>
        {
            ["evaluationId"] = evaluation.Id.ToString(),
            ["level"] = evaluation.Level.ToString(),
            ["eventType"] = eventType,
            ["latitude"] = source.Latitude.ToString(CultureInfo.InvariantCulture),
            ["longitude"] = source.Longitude.ToString(CultureInfo.InvariantCulture),
            ["catalogVersion"] = catalog!.Version.ToString(CultureInfo.InvariantCulture)
        };
        if (source.Intensity is { } intensity)
            metadata["intensity"] = intensity.ToString(CultureInfo.InvariantCulture);

        await _riskUpdatedPublisher.PublishAsync(new RiskUpdatedEvent(eventId, region.Code,
            eventType.ToLowerInvariant(), evaluation.Score, source.TenantId, evaluation.Timestamp, metadata));
    }

    // The catalog matrix can raise the level for an event type and intensity, never lower it.
    private static RiskLevel Floor(RiskLevel level, RiskLevel? matrixLevel) =>
        matrixLevel is { } floor && floor > level ? floor : level;

    private static bool IsRecent(DateTime calculatedAt) => DateTime.UtcNow - calculatedAt < TimeSpan.FromMinutes(5);
}