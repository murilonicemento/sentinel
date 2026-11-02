namespace Geospatial.Application.DTOs;

public class IntersectsRequestDTO
{
    public PolygonDTO PolygonA { get; set; } = new();
    public PolygonDTO PolygonB { get; set; } = new();
}