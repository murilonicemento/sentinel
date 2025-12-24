namespace RiskCatalog.Application.DTO;

public record SeverityCriterionDTO
{
    public string EventTypeCode { get; set; }
    public string SeverityLevel { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public string Unit { get; set; }
    public int Version { get; set; }
}