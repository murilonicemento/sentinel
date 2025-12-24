namespace RiskCatalog.Application.DTO;

public record RiskMatrixDTO
{
    public string EventTypeCode { get; set; }
    public string SeverityLevel { get; set; }
    public string RiskLevel { get; set; }
    public int Version { get; set; }
}