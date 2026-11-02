namespace Reporting.Domain.Entities;

public sealed record AnalyticsReport(
    string TenantId,
    int ProcessedEvents,
    int AlertCount,
    int SuccessfulDeliveries,
    int FailedDeliveries,
    IReadOnlyCollection<RegionMetric> RegionBreakdown,
    IReadOnlyCollection<ChannelMetric> ChannelBreakdown,
    double AverageRiskScore,
    double DeliveryRate = 0,
    double FailureRate = 0,
    double MaxRiskScore = 0,
    IReadOnlyCollection<SeverityMetric> SeverityBreakdown = null!,
    IReadOnlyCollection<TimeSeriesMetric> TimeSeriesBreakdown = null!);

public sealed record RegionMetric(string Region, int Count);

public sealed record ChannelMetric(string Channel, int Count);

public sealed record SeverityMetric(string Severity, int Count);

public sealed record TimeSeriesMetric(string Period, int Count);
