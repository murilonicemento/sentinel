namespace RiskCatalog.Application.DTO;

public record IdentityDTO
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string[] Roles { get; set; } = [];
}