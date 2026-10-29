namespace Geospatial.Application.DTOs;

public class WithinRadiusPayloadDTO
{
    public required PointDTO Center { get; set; }
    public required PointDTO Point { get; set; }
    public required RadiusDTO Radius { get; set; }
}