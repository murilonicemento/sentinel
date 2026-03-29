using RiskEvaluation.Domain.Enums;
using RiskEvaluation.Domain.Repositories;
using RiskEvaluation.Domain.ValueObjects;

namespace RiskEvaluation.Domain.Services;

/// <summary>
/// Service for calculating risk scores using external weights and geospatial context.
/// </summary>
public class RiskCalculationService
{
    private readonly IRiskFactorRepository? _riskFactorRepository;

    private readonly Dictionary<string, double> _defaultMetricWeights = new()
    {
        ["TemperatureAnomaly"] = 0.15,
        ["HumidityAnomaly"] = 0.10,
        ["WindGust"] = 0.15,
        ["Rainfall"] = 0.12,
        ["PressureChange"] = 0.08
    };

    private readonly Dictionary<string, double> _defaultEventWeights = new()
    {
        ["Wildfire"] = 0.10,
        ["Earthquake"] = 0.12,
        ["Flood"] = 0.10,
        ["Landslide"] = 0.08
    };

    public RiskCalculationService(IRiskFactorRepository? riskFactorRepository = null)
    {
        _riskFactorRepository = riskFactorRepository;
    }

    /// <summary>
    /// Load weights from RiskFactor repository or use defaults.
    /// </summary>
    public async Task<(Dictionary<string, double> MetricWeights, Dictionary<string, double> EventWeights)>
        LoadWeightsAsync(CancellationToken cancellationToken = default)
    {
        if (_riskFactorRepository == null)
            return (_defaultMetricWeights, _defaultEventWeights);

        var allFactors = await _riskFactorRepository.GetAllAsync(cancellationToken);

        var metricWeights = new Dictionary<string, double>();
        var eventWeights = new Dictionary<string, double>();

        foreach (var factor in allFactors)
        {
            var key = factor.Type.ToString();

            if (IsMetricType(factor.Type))
                metricWeights[key] = factor.Weight;
            else if (IsEventType(factor.Type))
                eventWeights[key] = factor.Weight;
        }

        // Fill missing with defaults
        foreach (var (key, value) in _defaultMetricWeights)
        {
            if (!metricWeights.ContainsKey(key))
                metricWeights[key] = value;
        }

        foreach (var (key, value) in _defaultEventWeights)
        {
            if (!eventWeights.ContainsKey(key))
                eventWeights[key] = value;
        }

        return (metricWeights, eventWeights);
    }

    private static bool IsMetricType(RiskFactorType type) => type switch
    {
        RiskFactorType.TemperatureAnomaly => true,
        RiskFactorType.HumidityAnomaly => true,
        RiskFactorType.WindGust => true,
        RiskFactorType.Rainfall => true,
        RiskFactorType.PressureChange => true,
        _ => false
    };

    private static bool IsEventType(RiskFactorType type) => type switch
    {
        RiskFactorType.Wildfire => true,
        RiskFactorType.Earthquake => true,
        RiskFactorType.Flood => true,
        RiskFactorType.Landslide => true,
        _ => false
    };

    /// <summary>
    /// Calculate risk score using external weights if provided.
    /// </summary>
    public double CalculateRiskScore(
        RiskMetrics metrics,
        RiskEvents events,
        Dictionary<string, double>? externalMetricWeights = null,
        Dictionary<string, double>? externalEventWeights = null,
        GeospatialContext? geoContext = null)
    {
        var metricWeights = externalMetricWeights ?? _defaultMetricWeights;
        var eventWeights = externalEventWeights ?? _defaultEventWeights;

        // Apply geospatial adjustments if context provided
        var geoMultiplier = CalculateGeoMultiplier(geoContext);

        // Continuous metrics contribution (0-0.6 range)
        var metricsScore = (
            Math.Clamp(metrics.TemperatureAnomaly / 10.0, 0, 1) * GetWeight(metricWeights, "TemperatureAnomaly", 0.15) +
            Math.Clamp(Math.Abs(metrics.HumidityAnomaly) / 50.0, 0, 1) *
            GetWeight(metricWeights, "HumidityAnomaly", 0.10) +
            Math.Clamp(metrics.WindGust / 100.0, 0, 1) * GetWeight(metricWeights, "WindGust", 0.15) +
            Math.Clamp(metrics.Rainfall / 50.0, 0, 1) * GetWeight(metricWeights, "Rainfall", 0.12) +
            Math.Clamp(Math.Abs(metrics.PressureChange) / 20.0, 0, 1) * GetWeight(metricWeights, "PressureChange", 0.08)
        );

        // Event intensities contribution (0-0.4 range)
        var eventsScore = 0.0;
        eventsScore += GetEventScore(events.Wildfire, GetWeight(eventWeights, "Wildfire", 0.10));
        eventsScore += GetEventScore(events.Earthquake, GetWeight(eventWeights, "Earthquake", 0.12));
        eventsScore += GetEventScore(events.Flood, GetWeight(eventWeights, "Flood", 0.10));
        eventsScore += GetEventScore(events.Landslide, GetWeight(eventWeights, "Landslide", 0.08));

        var totalScore = Math.Clamp((metricsScore + eventsScore) * geoMultiplier, 0.0, 1.0);
        return totalScore;
    }

    private static double GetWeight(Dictionary<string, double> weights, string key, double defaultValue) =>
        weights.GetValueOrDefault(key, defaultValue);


    private static double GetEventScore(EventDetails eventDetails, double maxWeight)
    {
        if (!eventDetails.Detected)
            return 0.0;

        // Intensity scale 0-10, normalized to weight
        var intensityFactor = eventDetails.Intensity.HasValue
            ? Math.Clamp(eventDetails.Intensity.Value / 10.0, 0, 1)
            : 0.5; // Default if no intensity provided

        return maxWeight * intensityFactor;
    }

    private static double CalculateGeoMultiplier(GeospatialContext? context)
    {
        if (context == null)
            return 1.0;

        var multiplier = 1.0;

        // Adjust based on terrain type
        multiplier += context.TerrainType?.ToLower() switch
        {
            "mountainous" => 0.15,
            "coastal" => 0.10,
            "floodplain" => 0.20,
            "urban" => 0.05,
            _ => 0.0
        };

        // Adjust based on elevation (higher elevation = higher risk for some events)
        if (context.Elevation > 1000)
            multiplier += 0.10;

        switch (context.ProximityToCoast)
        {
            // Adjust based on proximity to coast
            // Less than 5km
            case < 5000:
                multiplier += 0.15;
                break;
            // Less than 20km
            case < 20000:
                multiplier += 0.05;
                break;
        }

        // Adjust based on risk zones
        multiplier += context.RiskZones.Sum(zone => zone.ToLower() switch
        {
            "high" => 0.20,
            "medium" => 0.10,
            "low" => 0.05,
            _ => 0.0
        });

        return Math.Clamp(multiplier, 0.5, 2.0); // Cap between 0.5x and 2x
    }

    public static RiskLevel ClassifyRiskLevel(double score) => score switch
    {
        < 0.3 => RiskLevel.Low,
        < 0.6 => RiskLevel.Medium,
        < 0.8 => RiskLevel.High,
        _ => RiskLevel.Critical
    };
}