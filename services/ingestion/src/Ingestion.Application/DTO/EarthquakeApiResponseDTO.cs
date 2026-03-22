namespace Ingestion.Application.DTO;

public record EarthquakeApiResponseDTO
{
    public string Id { get; init; }
    public EarthquakeProperties Properties { get; init; }
    public EarthquakeGeometry Geometry { get; init; }
}

public record EarthquakeProperties
{
    public double? Mag { get; init; }
    public string MagType { get; init; }
    public string Place { get; init; }
    public long? Time { get; init; } // timestamp em ms UTC
    public string Url { get; init; }
    public string Status { get; init; }
    public int? Tsunami { get; init; }
    public int? Sig { get; init; }
    public string Net { get; init; }
    public string Code { get; init; }
    public string Title { get; init; }
}

public record EarthquakeGeometry
{
    public string Type { get; init; }
    public double[] Coordinates { get; init; } // [longitude, latitude, depth]
}

public record EarthquakeFeatureCollection
{
    public string Type { get; init; } // "FeatureCollection"
    public EarthquakeMetadata Metadata { get; init; }
    public List<EarthquakeApiResponseDTO> Features { get; init; }
}

public record EarthquakeMetadata
{
    public long Generated { get; init; }
    public string Url { get; init; }
    public string Title { get; init; }
    public int Status { get; init; }
    public string Api { get; init; }
    public int Count { get; init; }
}