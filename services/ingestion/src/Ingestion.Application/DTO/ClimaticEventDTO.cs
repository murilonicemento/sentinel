namespace Ingestion.Application.DTO;

public record ClimaticEventDTO(
    Guid EventId,
    string EventType,
    double Latitude,
    double Longitude,
    double Intensity,
    DateTime CollectedAt
)
{
    public Guid EventId { get; } = EventId;
    public string EventType { get; } = EventType;
    public double Latitude { get; } = Latitude;
    public double Longitude { get; } = Longitude;
    public double Intensity { get; } = Intensity;
    public DateTime CollectedAt { get; } = CollectedAt;
}