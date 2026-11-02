namespace Ingestion.Application.DTO;

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