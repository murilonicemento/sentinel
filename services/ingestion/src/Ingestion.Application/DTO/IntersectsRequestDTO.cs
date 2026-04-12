namespace Ingestion.Application.DTO;

public class IntersectsRequestDTO
{
    public PolygonDTO PolygonA { get; set; } = new();
    public PolygonDTO PolygonB { get; set; } = new();
}