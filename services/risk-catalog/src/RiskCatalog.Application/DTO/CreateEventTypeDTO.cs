using System.ComponentModel.DataAnnotations;

namespace RiskCatalog.Application.DTO;

public record CreateEventTypeDTO
{
    [Required] public string Code { get; set; }
    [Required] public string Name { get; set; }
    [Required] public string Description { get; set; }
    public bool IsActive { get; set; } = false;
}