using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Ingestion.Infrastructure.Read.Persistence.Documents;

/// <summary>
/// Read-model projection stored in climaticEvents / disasterEvents. Documents written before EventId and TenantId
/// were projected deserialize with empty identifiers and are visible only to the platform scope.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class DetectedEventDocument
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid EventId { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid CollectionId { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid TenantId { get; set; }

    /// <summary>Not persisted: set by the query pipeline from the source collection.</summary>
    [BsonIgnoreIfNull]
    public string? Domain { get; set; }

    public string EventType { get; set; } = string.Empty;
    public double Intensity { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CollectedAt { get; set; }
}

public sealed class EventTypeStatisticsDocument
{
    public string EventType { get; set; } = string.Empty;
    public int Count { get; set; }
    public double MinIntensity { get; set; }
    public double MaxIntensity { get; set; }
    public double TotalIntensity { get; set; }
}
