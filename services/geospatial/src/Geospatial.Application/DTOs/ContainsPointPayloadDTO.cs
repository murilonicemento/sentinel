namespace Geospatial.Application.DTOs;

public class ContainsPointPayloadDTO
{
    public PointDTO Point { get; set; }
    public PolygonDTO Area { get; set; }
}