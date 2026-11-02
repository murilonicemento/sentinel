namespace Geospatial.Application.DTOs;

public class ContainsPointRequestDTO
{
    public PointDTO Point { get; set; } = new();
    public PolygonDTO Area { get; set; } = new();
}