using System.Text.Json.Serialization;

namespace RiskEvaluation.Domain.ValueObjects;

/// <summary>
/// Represents continuous risk metrics with double values.
/// </summary>
public class RiskMetrics
{
    [JsonPropertyName("temperatureAnomaly")]
    public double TemperatureAnomaly { get; set; }

    [JsonPropertyName("humidityAnomaly")]
    public double HumidityAnomaly { get; set; }

    [JsonPropertyName("windGust")]
    public double WindGust { get; set; }

    [JsonPropertyName("rainfall")]
    public double Rainfall { get; set; }

    [JsonPropertyName("pressureChange")]
    public double PressureChange { get; set; }
}

/// <summary>
/// Represents individual disaster event with detection and intensity.
/// </summary>
public class EventDetails
{
    [JsonPropertyName("detected")]
    public bool Detected { get; set; }

    [JsonPropertyName("intensity")]
    public double? Intensity { get; set; }
}

/// <summary>
/// Represents discrete risk events with detection and intensity for each type.
/// </summary>
public class RiskEvents
{
    [JsonPropertyName("wildfire")]
    public EventDetails Wildfire { get; set; } = new();

    [JsonPropertyName("earthquake")]
    public EventDetails Earthquake { get; set; } = new();

    [JsonPropertyName("flood")]
    public EventDetails Flood { get; set; } = new();

    [JsonPropertyName("landslide")]
    public EventDetails Landslide { get; set; } = new();
}

/// <summary>
/// Unified risk factors container with both continuous metrics and discrete events.
/// </summary>
public class RiskFactors
{
    [JsonPropertyName("metrics")]
    public RiskMetrics Metrics { get; set; } = new();

    [JsonPropertyName("events")]
    public RiskEvents Events { get; set; } = new();
}
