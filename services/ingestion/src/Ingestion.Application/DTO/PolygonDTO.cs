namespace Ingestion.Application.DTO;

public record PolygonDTO()
{
    public List<PointDTO> Coordinates { get; set; } = [];
}