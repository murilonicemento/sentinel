namespace RiskEvaluation.Application.DTOs;

public class NearbyRiskDTO
{
    public string RiskType { get; set; } = string.Empty;
    public double Distance { get; set; }
    public int Latitude { get; set; }
    public int Longitude { get; set; }
    public double Severity { get; set; }
}
