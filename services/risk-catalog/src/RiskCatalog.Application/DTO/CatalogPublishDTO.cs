using System.ComponentModel.DataAnnotations;

namespace RiskCatalog.Application.DTO;

public record CatalogPublishDTO
{
    [Required] public int Version { get; set; }
    [Required] public string Notes { get; set; }
}