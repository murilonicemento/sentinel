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