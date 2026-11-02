namespace Geospatial.Application.DTOs;

public class SearchByPolygonRequestDTO
{
    public List<PointDTO> Polygon { get; set; } = [];
}