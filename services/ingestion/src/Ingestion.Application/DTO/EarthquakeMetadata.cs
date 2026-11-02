namespace Ingestion.Application.DTO;

public record EarthquakeMetadata
{
    public long Generated { get; init; }
    public string Url { get; init; }
    public string Title { get; init; }
    public int Status { get; init; }
    public string Api { get; init; }
    public int Count { get; init; }
}