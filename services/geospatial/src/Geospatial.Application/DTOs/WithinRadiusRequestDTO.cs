namespace Geospatial.Application.DTOs;

public class WithinRadiusRequestDTO
{
    public PointDTO Center { get; set; } = new();
    public PointDTO Point { get; set; } = new();
    public RadiusDTO Radius { get; set; } = new();
}