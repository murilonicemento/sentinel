using System.Text.Json.Serialization;

namespace RiskEvaluation.Domain.ValueObjects;

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