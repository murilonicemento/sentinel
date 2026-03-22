namespace Geospatial.Application.DTOs;

public class DistanceRequestDTO
{
    public PointDTO From { get; set; } = new();
    public PointDTO To { get; set; } = new();
}