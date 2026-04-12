namespace Ingestion.Application.DTO;

public record EarthquakeFeatureCollection
{
    public string Type { get; init; } // "FeatureCollection"
    public EarthquakeMetadata Metadata { get; init; }
    public List<EarthquakeApiResponseDTO> Features { get; init; }
}