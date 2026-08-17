namespace TenantsBilling.Infrastructure.Persistence.Entities;

public sealed class TenantEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public int Status { get; set; }
    public string Region { get; set; } = string.Empty;
    public string TimeZone { get; set; } = "UTC";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
