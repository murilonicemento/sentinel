namespace Ingestion.Application.DTO;

public record EarthquakeApiResponseDTO
{
    public string Id { get; init; }
    public EarthquakeProperties Properties { get; init; }
    public EarthquakeGeometry Geometry { get; init; }
}