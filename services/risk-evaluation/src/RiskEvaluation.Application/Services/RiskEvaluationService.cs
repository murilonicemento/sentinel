using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Application.IntegrationClients;
using RiskEvaluation.Domain.Enums;
using RiskEvaluation.Domain.Events;
using RiskEvaluation.Domain.Interfaces;
using RiskEvaluation.Domain.Repositories;
using RiskEvaluation.Domain.Services;
using RiskEvaluation.Domain.ValueObjects;

namespace RiskEvaluation.Application.Services;

public class RiskEvaluationService : IRiskEvaluationService
{
    private readonly IRiskEvaluationRepository _repository;
    private readonly IEventPublisher _eventPublisher;
    private readonly RiskCalculationService _calculationService;
    private readonly IRecentScoresCache _cache;
    private readonly IRiskCatalogClient _riskCatalogClient;
    private readonly IGeospatialClient _geospatialClient;
    private readonly ILogger<RiskEvaluationService> _logger;

    public RiskEvaluationService(
        IRiskEvaluationRepository repository,
        IEventPublisher eventPublisher,
        RiskCalculationService calculationService,
        IRecentScoresCache cache,
        IRiskCatalogClient riskCatalogClient,
        IGeospatialClient geospatialClient,
        ILogger<RiskEvaluationService> logger)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
        _calculationService = calculationService;
        _cache = cache;
        _riskCatalogClient = riskCatalogClient;
        _geospatialClient = geospatialClient;
        _logger = logger;
    }

    public async Task<RiskEvaluationEntity> EvaluateRiskAsync(
        int latitude,
        int longitude,
        RiskMetrics metrics,
        RiskEvents events)
    {
        var cached = await _cache.GetAsync(latitude, longitude);
        if (cached != null && IsRecent(cached.CalculatedAt))
        {
            var cachedLevel = Enum.Parse<RiskLevel>(cached.Level);
            var cachedEvaluation = new RiskEvaluationEntity(latitude, longitude, cached.Score, cachedLevel);
            await _repository.AddAsync(cachedEvaluation);
            return cachedEvaluation;
        }

        // Fetch external weights from Risk Catalog
        var riskWeights = await _riskCatalogClient.GetLatestRiskWeightsAsync();
        var metricWeights = riskWeights?.MetricWeights;
        var eventWeights = riskWeights?.EventWeights;

        // Fetch geospatial context
        var geoContextDto = await _geospatialClient.GetSpatialContextAsync(latitude, longitude);
        GeospatialContext? geoContext = null;
        if (geoContextDto != null)
        {
            geoContext = new GeospatialContext
            {
                Latitude = geoContextDto.Latitude,
                Longitude = geoContextDto.Longitude,
                RegionId = geoContextDto.RegionId,
                TerrainType = geoContextDto.TerrainType,
                Elevation = geoContextDto.Elevation,
                ProximityToCoast = geoContextDto.ProximityToCoast,
                RiskZones = geoContextDto.RiskZones
            };
        }

        var score = _calculationService.CalculateRiskScore(metrics, events, metricWeights, eventWeights, geoContext);
        var level = _calculationService.ClassifyRiskLevel(score);

        await _cache.SetAsync(latitude, longitude, new RiskScoreCacheEntry
        {
            Score = score,
            Level = level.ToString(),
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

    private static bool IsRecent(DateTime calculatedAt) => DateTime.UtcNow - calculatedAt < TimeSpan.FromMinutes(5);
}