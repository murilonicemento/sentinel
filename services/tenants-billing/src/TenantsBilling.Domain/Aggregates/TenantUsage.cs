using TenantsBilling.Domain.Enums;

namespace TenantsBilling.Domain.Aggregates;

/// <summary>
/// Usage accumulated by a tenant in one billing period (calendar month in the tenant's time zone), compared with the
/// plan limits. The quota status is the highest consumption percentage among the four metered resources.
/// </summary>
public sealed class TenantUsage
{
    private TenantUsage()
    {
        Period = string.Empty;
    }

    private TenantUsage(Guid tenantId, string period)
    {
        TenantId = tenantId;
        Period = period;
    }

    public Guid TenantId { get; private set; }

    /// <summary>Billing period as <c>yyyy-MM</c>.</summary>
    public string Period { get; private set; }

    public long EventsConsumed { get; private set; }
    public long AlertsTriggered { get; private set; }
    public long ApiRequests { get; private set; }
    public long ChannelUsage { get; private set; }
    public TenantQuotaStatus QuotaStatus { get; private set; } = TenantQuotaStatus.WithinLimit;
    public DateTime UpdatedAt { get; private set; }

    public static TenantUsage Start(Guid tenantId, string period)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(period)) throw new ArgumentException("Period is required.", nameof(period));
        return new TenantUsage(tenantId, period);
    }

    public static TenantUsage Rehydrate(Guid tenantId, string period, long eventsConsumed, long alertsTriggered,
        long apiRequests, long channelUsage, TenantQuotaStatus quotaStatus, DateTime updatedAt) => new(tenantId, period)
        {
            EventsConsumed = eventsConsumed,
            AlertsTriggered = alertsTriggered,
            ApiRequests = apiRequests,
            ChannelUsage = channelUsage,
            QuotaStatus = quotaStatus,
            UpdatedAt = updatedAt
        };

    /// <summary>Adds the consumption and re-evaluates the quota against <paramref name="plan"/>.</summary>
    public UsageRecordResult Record(int eventsConsumed, int alertsTriggered, int apiRequests, int channelUsage,
        Plan plan, DateTime recordedAt)
    {
        if (eventsConsumed < 0 || alertsTriggered < 0 || apiRequests < 0 || channelUsage < 0)
            throw new ArgumentOutOfRangeException(nameof(eventsConsumed), "Usage values cannot be negative.");

        EventsConsumed += eventsConsumed;
        AlertsTriggered += alertsTriggered;
        ApiRequests += apiRequests;
        ChannelUsage += channelUsage;
        UpdatedAt = recordedAt;

        var percentage = new[]
        {
            Percentage(EventsConsumed, plan.MaxEventsPerMonth),
            Percentage(AlertsTriggered, plan.MaxAlertsPerMonth),
            Percentage(ApiRequests, plan.MaxApiRequestsPerMonth),
            Percentage(ChannelUsage, plan.MaxChannelsPerMonth)
        }.Max();

        var status = percentage >= plan.HardLimitPercentage
            ? TenantQuotaStatus.HardLimit
            : percentage >= plan.SoftLimitPercentage
                ? TenantQuotaStatus.SoftLimit
                : TenantQuotaStatus.WithinLimit;

        // The status never goes back within a period: limits only reset with the next period.
        var escalated = status > QuotaStatus;
        if (escalated) QuotaStatus = status;

        return new UsageRecordResult(QuotaStatus, escalated, percentage);
    }

    private static double Percentage(long used, int limit) => used * 100d / limit;
}

/// <param name="Escalated">True when this record moved the period to a higher quota status.</param>
public sealed record UsageRecordResult(TenantQuotaStatus Status, bool Escalated, double UsagePercentage);
