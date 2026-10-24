namespace TenantsBilling.Domain.Aggregates;

public sealed class Plan
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int MaxEventsPerMonth { get; private set; }
    public int MaxAlertsPerMonth { get; private set; }
    public int MaxApiRequestsPerMonth { get; private set; }
    public int MaxChannelsPerMonth { get; private set; }
    public int SoftLimitPercentage { get; private set; } = 80;
    public int HardLimitPercentage { get; private set; } = 100;
    public bool IsActive { get; private set; } = true;

    private Plan() { }

    public Plan(
        Guid id,
        string name,
        int maxEventsPerMonth,
        int maxAlertsPerMonth,
        int maxApiRequestsPerMonth,
        int maxChannelsPerMonth,
        int softLimitPercentage = 80,
        int hardLimitPercentage = 100)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Plan id is required.", nameof(id));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Plan name is required.", nameof(name));

        if (maxEventsPerMonth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxEventsPerMonth));

        if (maxAlertsPerMonth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxAlertsPerMonth));

        if (maxApiRequestsPerMonth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxApiRequestsPerMonth));

        if (maxChannelsPerMonth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxChannelsPerMonth));

        if (softLimitPercentage <= 0 || softLimitPercentage >= 100)
            throw new ArgumentOutOfRangeException(nameof(softLimitPercentage));

        if (hardLimitPercentage <= softLimitPercentage || hardLimitPercentage > 100)
            throw new ArgumentOutOfRangeException(nameof(hardLimitPercentage));

        Id = id;
        Name = name.Trim();
        MaxEventsPerMonth = maxEventsPerMonth;
        MaxAlertsPerMonth = maxAlertsPerMonth;
        MaxApiRequestsPerMonth = maxApiRequestsPerMonth;
        MaxChannelsPerMonth = maxChannelsPerMonth;
        SoftLimitPercentage = softLimitPercentage;
        HardLimitPercentage = hardLimitPercentage;
    }
}
