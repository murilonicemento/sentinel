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

    /// <summary>
    /// Whether the tenant may move to <paramref name="target"/>: an active tenant can be suspended, a suspended or
    /// pending one activated, and any tenant canceled. Canceled is terminal. Keeping the current status is allowed.
    /// </summary>
    public bool CanChangeStatusTo(TenantStatus target)
    {
        if (target == Status) return true;
        if (Status == TenantStatus.Canceled) return false;

        return target switch
        {
            TenantStatus.Active => Status is TenantStatus.Suspended or TenantStatus.Pending,
            TenantStatus.Suspended => Status == TenantStatus.Active,
            TenantStatus.Canceled => true,
            _ => false
        };
    }

    /// <summary>Moves the tenant to <paramref name="target"/>; false when it already had that status.</summary>
    public bool ChangeStatus(TenantStatus target)
    {
        if (!CanChangeStatusTo(target))
            throw new InvalidOperationException($"A {Status} tenant cannot become {target}.");

        if (target == Status) return false;

        Status = target;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>Suspended and canceled tenants are over quota regardless of their consumption.</summary>
    public bool IsBlocked => Status is TenantStatus.Suspended or TenantStatus.Canceled;
}
