namespace TenantsBilling.Infrastructure.Persistence.Entities;

public sealed class PlanEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MaxEventsPerMonth { get; set; }
    public int MaxAlertsPerMonth { get; set; }
    public int MaxApiRequestsPerMonth { get; set; }
    public int MaxChannelsPerMonth { get; set; }
    public int SoftLimitPercentage { get; set; }
    public int HardLimitPercentage { get; set; }
    public bool IsActive { get; set; }
}
