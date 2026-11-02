namespace Geospatial.Application.DTOs;

public class WithinRadiusPayloadDTO
{
    public PointDTO Center { get; set; }
    public PointDTO Point { get; set; }
    public RadiusDTO Radius { get; set; }
}