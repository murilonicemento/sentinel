using TenantsBilling.Domain.Enums;

namespace TenantsBilling.Domain.Aggregates;

public sealed class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public Guid PlanId { get; private set; }
    public TenantStatus Status { get; private set; }
    public string Region { get; private set; }
    public string TimeZone { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Tenant()
    {
        Name = string.Empty;
        Region = string.Empty;
        TimeZone = string.Empty;
    }

    private Tenant(string name, Guid planId, string? region, string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tenant name is required.", nameof(name));

        if (planId == Guid.Empty)
            throw new ArgumentException("Plan id is required.", nameof(planId));

        Id = Guid.NewGuid();
        Name = name.Trim();
        PlanId = planId;
        Status = TenantStatus.Active;
        Region = string.IsNullOrWhiteSpace(region) ? "global" : region.Trim();
        TimeZone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone.Trim();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    internal Tenant(Guid id, string name, Guid planId, TenantStatus status, string region, string timeZone, DateTime createdAt, DateTime updatedAt)
    {
        Id = id;
        Name = name;
        PlanId = planId;
        Status = status;
        Region = region;
        TimeZone = timeZone;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Tenant Create(string name, Guid planId, string? region = null, string? timeZone = null)
    {
        return new Tenant(name, planId, region, timeZone);
    }

    public static Tenant Rehydrate(Guid id, string name, Guid planId, TenantStatus status, string region, string timeZone, DateTime createdAt, DateTime updatedAt)
    {
        return new Tenant(id, name, planId, status, region, timeZone, createdAt, updatedAt);
    }

    public void ChangePlan(Guid newPlanId)
    {
        if (newPlanId == Guid.Empty)
            throw new ArgumentException("Plan id is required.", nameof(newPlanId));

        PlanId = newPlanId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Suspend()
    {
        Status = TenantStatus.Suspended;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        Status = TenantStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Suspended and canceled tenants are over quota regardless of their consumption.</summary>
    public bool IsBlocked => Status is TenantStatus.Suspended or TenantStatus.Canceled;
}
