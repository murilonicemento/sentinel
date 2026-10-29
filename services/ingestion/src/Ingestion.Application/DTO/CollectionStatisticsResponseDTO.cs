using System.Text.Json.Serialization;
using Ingestion.Domain.Enums;

namespace Ingestion.Application.DTO;

public record CollectionStatisticsResponseDTO
{
    public int TotalEvents { get; set; }

    [JsonIgnore]
    public List<ClimaticEventEnum> TotalByTypeRaw { get; set; } = [];

    public Dictionary<string, int> TotalByType =>
        TotalByTypeRaw
            .GroupBy(t => t)
            .ToDictionary(g => g.Key.GetDisplayName(), g => g.Count());

    public double MinIntensity { get; set; }
    public double MaxIntensity { get; set; }
    public double AverageIntensity { get; set; }
}