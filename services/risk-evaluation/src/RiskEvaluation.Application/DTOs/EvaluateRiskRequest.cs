using System.ComponentModel.DataAnnotations;

namespace RiskEvaluation.Application.DTOs;

public class EvaluateRiskRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Location { get; set; } = string.Empty;

    [Required]
    public DateTime Timestamp { get; set; }

    [Required]
    public Dictionary<string, double> Metrics { get; set; } = new();
}