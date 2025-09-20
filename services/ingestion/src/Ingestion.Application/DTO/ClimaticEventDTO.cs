namespace Ingestion.Application.DTO;

public record ClimaticEventDTO
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Intensity { get; set; }
    public DateTime CollectedAt { get; set; }
}