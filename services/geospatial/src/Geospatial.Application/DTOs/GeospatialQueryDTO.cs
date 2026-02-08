namespace Geospatial.Application.DTOs;

public class SearchByRadiusRequestDTO
{
    public PointDTO Center { get; set; } = new();
    public double RadiusMeters { get; set; }
}

public class SearchByPolygonRequestDTO
{
    public List<PointDTO> Polygon { get; set; } = [];
}
