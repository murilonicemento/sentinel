namespace RiskCatalog.Application.DTO;

public record EventTypeDTO
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}