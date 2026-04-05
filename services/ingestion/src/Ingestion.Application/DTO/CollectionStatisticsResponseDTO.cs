using System.Text.Json.Serialization;
using Ingestion.Domain.Enums;
using MongoDB.Bson.Serialization.Attributes;

namespace Ingestion.Application.DTO;

public record CollectionStatisticsResponseDTO
{
    [BsonElement("TotalEvents")] public int TotalEvents { get; set; }

    [BsonElement("TotalByType")]
    [JsonIgnore]
    public List<ClimaticEventEnum> TotalByTypeRaw { get; set; }

    public Dictionary<string, int> TotalByType =>
        TotalByTypeRaw
            .GroupBy(t => t)
            .ToDictionary(g => g.Key.GetDisplayName(), g => g.Count());

    [BsonElement("MinIntensity")] public double MinIntensity { get; set; }
    [BsonElement("MaxIntensity")] public double MaxIntensity { get; set; }
    [BsonElement("AverageIntensity")] public double AverageIntensity { get; set; }
}