namespace RiskCatalog.Application.DTO;

public record CreateEventTypeDTO
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; } = false;
}