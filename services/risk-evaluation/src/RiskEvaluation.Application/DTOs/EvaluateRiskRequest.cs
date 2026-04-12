using System.ComponentModel.DataAnnotations;
using RiskEvaluation.Domain.ValueObjects;

namespace RiskEvaluation.Application.DTOs;

public class EvaluateRiskRequest
{
    [Required]
    [Range(-90, 90)]
    public int Latitude { get; set; }

    [Required]
    [Range(-180, 180)]
    public int Longitude { get; set; }

    [Required]
    public DateTime Timestamp { get; set; }

    [Required]
    public RiskMetrics Metrics { get; set; } = new();

    public RiskEvents Events { get; set; } = new();
}