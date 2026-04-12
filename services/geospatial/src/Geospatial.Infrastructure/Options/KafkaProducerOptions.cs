namespace Geospatial.Infrastructure.Options;

public sealed class KafkaProducerOptions
{
    public string RegionIntersectedTopic { get; init; } = "region-intersected";
}
