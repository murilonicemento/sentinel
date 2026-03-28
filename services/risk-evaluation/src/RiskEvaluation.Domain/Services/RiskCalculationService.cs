using RiskEvaluation.Domain.Enums;
using RiskEvaluation.Domain.ValueObjects;

namespace RiskEvaluation.Domain.Services;

/// <summary>
/// Service for calculating risk scores using external weights and geospatial context.
/// </summary>
public class RiskCalculationService
{
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
        double geoMultiplier = CalculateGeoMultiplier(geoContext);

        // Continuous metrics contribution (0-0.6 range)
        double metricsScore = (
            Math.Clamp(metrics.TemperatureAnomaly / 10.0, 0, 1) * GetWeight(metricWeights, "TemperatureAnomaly", 0.15) +
            Math.Clamp(Math.Abs(metrics.HumidityAnomaly) / 50.0, 0, 1) * GetWeight(metricWeights, "HumidityAnomaly", 0.10) +
            Math.Clamp(metrics.WindGust / 100.0, 0, 1) * GetWeight(metricWeights, "WindGust", 0.15) +
            Math.Clamp(metrics.Rainfall / 50.0, 0, 1) * GetWeight(metricWeights, "Rainfall", 0.12) +
            Math.Clamp(Math.Abs(metrics.PressureChange) / 20.0, 0, 1) * GetWeight(metricWeights, "PressureChange", 0.08)
        );

        // Event intensities contribution (0-0.4 range)
        double eventsScore = 0.0;
        eventsScore += GetEventScore(events.Wildfire, GetWeight(eventWeights, "Wildfire", 0.10));
        eventsScore += GetEventScore(events.Earthquake, GetWeight(eventWeights, "Earthquake", 0.12));
        eventsScore += GetEventScore(events.Flood, GetWeight(eventWeights, "Flood", 0.10));
        eventsScore += GetEventScore(events.Landslide, GetWeight(eventWeights, "Landslide", 0.08));

        double totalScore = Math.Clamp((metricsScore + eventsScore) * geoMultiplier, 0.0, 1.0);
        return totalScore;
    }

    private static double GetWeight(Dictionary<string, double> weights, string key, double defaultValue)
    {
        return weights.TryGetValue(key, out var weight) ? weight : defaultValue;
    }

    private static double GetEventScore(EventDetails eventDetails, double maxWeight)
    {
        if (!eventDetails.Detected)
            return 0.0;

        // Intensity scale 0-10, normalized to weight
        double intensityFactor = eventDetails.Intensity.HasValue
            ? Math.Clamp(eventDetails.Intensity.Value / 10.0, 0, 1)
            : 0.5; // Default if no intensity provided

        return maxWeight * intensityFactor;
    }

    private static double CalculateGeoMultiplier(GeospatialContext? context)
    {
        if (context == null)
            return 1.0;

        double multiplier = 1.0;

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

        // Adjust based on proximity to coast
        if (context.ProximityToCoast < 5000) // Less than 5km
            multiplier += 0.15;
        else if (context.ProximityToCoast < 20000) // Less than 20km
            multiplier += 0.05;

        // Adjust based on risk zones
        foreach (var zone in context.RiskZones)
        {
            multiplier += zone.ToLower() switch
            {
                "high" => 0.20,
                "medium" => 0.10,
                "low" => 0.05,
                _ => 0.0
            };
        }

        return Math.Clamp(multiplier, 0.5, 2.0); // Cap between 0.5x and 2x
    }

    public RiskLevel ClassifyRiskLevel(double score)
    {
        if (score < 0.3) return RiskLevel.Low;
        if (score < 0.6) return RiskLevel.Medium;
        if (score < 0.8) return RiskLevel.High;
        return RiskLevel.Critical;
    }
}

/// <summary>
/// Geospatial context for risk calculation adjustments.
/// </summary>
public class GeospatialContext
{
    public int Latitude { get; set; }
    public int Longitude { get; set; }
    public string? RegionId { get; set; }
    public string? TerrainType { get; set; }
    public double Elevation { get; set; }
    public double ProximityToCoast { get; set; }
    public List<string> RiskZones { get; set; } = new();
}