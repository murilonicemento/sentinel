namespace RiskCatalog.Application.DTO;

public record EventTypeDTO
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
}