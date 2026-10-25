using Ingestion.Domain.Enums;
using MongoDB.Bson.Serialization.Attributes;

namespace Ingestion.Infrastructure.Read.Persistence.Documents;

internal sealed class CollectionStatisticsDocument
{
    [BsonElement("TotalEvents")] public int TotalEvents { get; set; }

    [BsonElement("TotalByType")] public List<ClimaticEventEnum> TotalByTypeRaw { get; set; } = [];

    [BsonElement("MinIntensity")] public double MinIntensity { get; set; }
    [BsonElement("MaxIntensity")] public double MaxIntensity { get; set; }
    [BsonElement("AverageIntensity")] public double AverageIntensity { get; set; }
}
