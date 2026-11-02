using System.ComponentModel.DataAnnotations;

namespace RiskEvaluation.Application.DTOs;

public class UpdateRiskModelRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Version { get; set; } = string.Empty;

    public Dictionary<string, double> Parameters { get; set; } = new();

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Formula { get; set; } = string.Empty;
}