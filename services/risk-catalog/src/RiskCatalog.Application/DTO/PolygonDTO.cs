namespace RiskCatalog.Application.DTO;

public record PolygonDTO()
{
    public List<PointDTO> Coordinates { get; set; } = [];
}