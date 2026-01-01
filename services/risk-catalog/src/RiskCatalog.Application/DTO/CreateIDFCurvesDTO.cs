namespace RiskCatalog.Application.DTO;

public record CreateIDFCurvesDTO
{
    public string EventTypeCode { get; set; }
    public int DurationMinutes { get; set; }
    public double Intensity { get; set; }
    public int ReturnPeriodYears { get; set; }
    public int Version { get; set; }
}