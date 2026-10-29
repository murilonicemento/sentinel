using System.ComponentModel.DataAnnotations;

namespace RiskCatalog.Application.DTO;

public record CreateEventTypeDTO
{
    [Required] public string Code { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = false;
}