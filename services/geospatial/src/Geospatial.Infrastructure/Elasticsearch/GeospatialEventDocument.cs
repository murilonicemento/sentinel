using Nest;

namespace Geospatial.Infrastructure.Elasticsearch;

[ElasticsearchType(RelationName = "geospatial_event")]
public class GeospatialEventDocument
{
    [Keyword]
    public Guid EventId { get; set; }

    [Keyword]
    public string OperationType { get; set; } = string.Empty;

    [Text(Index = false)]
    public string? Payload { get; set; }

    [Text(Index = false)]
    public string? Result { get; set; }

    [Date]
    public DateTimeOffset Timestamp { get; set; }

    [GeoPoint]
    public Nest.GeoLocation? MainPoint { get; set; }
}
