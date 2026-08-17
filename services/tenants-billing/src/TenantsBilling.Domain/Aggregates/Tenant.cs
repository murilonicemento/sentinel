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

    public TenantQuotaStatus RecordUsage(int eventsConsumed, int alertsTriggered, int apiRequests, int channelUsage)
    {
        if (Status == TenantStatus.Suspended || Status == TenantStatus.Canceled)
            return TenantQuotaStatus.HardLimit;

        var softLimitReached = eventsConsumed >= 80 || alertsTriggered >= 80 || apiRequests >= 80 || channelUsage >= 80;
        var hardLimitReached = eventsConsumed >= 100 || alertsTriggered >= 100 || apiRequests >= 100 || channelUsage >= 100;

        if (hardLimitReached)
            return TenantQuotaStatus.HardLimit;

        if (softLimitReached)
            return TenantQuotaStatus.SoftLimit;

        return TenantQuotaStatus.WithinLimit;
    }
}
