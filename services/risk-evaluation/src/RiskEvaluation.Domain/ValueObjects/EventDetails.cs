using System.Text.Json.Serialization;

namespace RiskEvaluation.Domain.ValueObjects;

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