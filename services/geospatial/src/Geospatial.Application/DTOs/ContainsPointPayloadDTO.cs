namespace Geospatial.Application.DTOs;

public class ContainsPointPayloadDTO
{
    public required PointDTO Point { get; set; }
    public required PolygonDTO Area { get; set; }
}