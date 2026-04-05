namespace Ingestion.Application.DTO;

public record EarthquakeGeometry
{
    public string Type { get; init; }
    public double[] Coordinates { get; init; } // [longitude, latitude, depth]
}