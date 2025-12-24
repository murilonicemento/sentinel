namespace RiskCatalog.Application.DTO;

public record RegionalRiskParametersDTO
{
    public Guid RegionId { get; set; }
    public double AdjustmentFactor { get; set; }
    public string Description { get; set; }
}