using Reporting.Domain.Entities;

namespace Reporting.Application.Models;

public sealed record DashboardSummaryDTO(
    string TenantId,
    int ProcessedEvents,
    int TotalAlerts,
    int SuccessfulDeliveries,
    double AverageRiskScore)
{
    public static DashboardSummaryDTO From(DashboardSummary summary) => new(
        summary.TenantId,
        summary.ProcessedEvents,
        summary.TotalAlerts,
        summary.SuccessfulDeliveries,
        summary.AverageRiskScore);
}

public sealed record MetricCountDTO(string Key, int Count);

public sealed record AnalyticsReportDTO(
    string TenantId,
    int ProcessedEvents,
    int AlertCount,
    int SuccessfulDeliveries,
    int FailedDeliveries,
    double AverageRiskScore,
    double MaxRiskScore,
    double DeliveryRate,
    double FailureRate,
    IReadOnlyList<MetricCountDTO> Regions,
    IReadOnlyList<MetricCountDTO> Channels,
    IReadOnlyList<MetricCountDTO> Severities,
    IReadOnlyList<MetricCountDTO> TimeSeries)
{
    public static AnalyticsReportDTO From(AnalyticsReport report) => new(
        report.TenantId,
        report.ProcessedEvents,
        report.AlertCount,
        report.SuccessfulDeliveries,
        report.FailedDeliveries,
        report.AverageRiskScore,
        report.MaxRiskScore,
        report.DeliveryRate,
        report.FailureRate,
        report.RegionBreakdown.Select(m => new MetricCountDTO(m.Region, m.Count)).ToList(),
        report.ChannelBreakdown.Select(m => new MetricCountDTO(m.Channel, m.Count)).ToList(),
        (report.SeverityBreakdown ?? []).Select(m => new MetricCountDTO(m.Severity, m.Count)).ToList(),
        (report.TimeSeriesBreakdown ?? []).Select(m => new MetricCountDTO(m.Period, m.Count)).ToList());
}

public sealed record ProcessingStatusDTO(
    string Status,
    DateTime Timestamp,
    int ProcessedEvents,
    int RejectedEvents)
{
    public static ProcessingStatusDTO From(ServiceHealthSnapshot snapshot) => new(
        snapshot.Status,
        snapshot.Timestamp,
        snapshot.ProcessedEvents,
        snapshot.RejectedEvents);
}

public sealed record ReplayResultDTO(string Topic, int Partition, long Offset, string SourceTopic, string RequestedBy)
{
    public static ReplayResultDTO From(ReplayPublishResult result) =>
        new(result.Topic, result.Partition, result.Offset, result.SourceTopic, result.RequestedBy);
}

public sealed record ResponseBaseDTO<T>
{
    public int StatusCode { get; init; }
    public bool Success { get; init; }
    public required T Data { get; init; }
}
