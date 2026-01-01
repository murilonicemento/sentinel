namespace RiskCatalog.Application.DTO;

public record CatalogPublishDTO
{
    public int Version { get; set; }
    public string Notes { get; set; }
}