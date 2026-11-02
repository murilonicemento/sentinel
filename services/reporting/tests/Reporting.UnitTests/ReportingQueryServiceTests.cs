using Microsoft.Extensions.Caching.Memory;
using Reporting.Application.Services;
using Reporting.Domain.Entities;
using Reporting.Domain.Interfaces;

namespace Reporting.UnitTests;

public class ReportingQueryServiceTests
{
    [Fact]
    public async Task GetDashboardSummary_ReturnsAggregatedValues()
    {
        var repository = new FakeReportingRepository();
        var sut = new ReportingQueryService(repository);

        await repository.ProcessEventAsync(new ReportingEvent(
            "evt-1",
            "AlertTriggered",
            "tenant-a",
            "north",
            "High",
            "sms",
            0.82,
            "Success"));

        await repository.ProcessEventAsync(new ReportingEvent(
            "evt-2",
            "NotificationSent",
            "tenant-a",
            "north",
            "High",
            "sms",
            0.82,
            "Success"));

        var summary = await sut.GetDashboardSummaryAsync("tenant-a");

        Assert.Equal(2, summary.ProcessedEvents);
        Assert.Equal(1, summary.TotalAlerts);
        Assert.Equal(1, summary.SuccessfulDeliveries);
        Assert.Equal(0.82, summary.AverageRiskScore, 2);
    }

    [Fact]
    public async Task GetDashboardSummary_UsesCacheForRepeatedRequests()
    {
        var repository = new FakeReportingRepository();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new ReportingQueryService(repository, cache);

        await repository.ProcessEventAsync(new ReportingEvent("evt-1", "AlertTriggered", "tenant-a", "north", "High", "sms", 0.82, "Success"));

        await sut.GetDashboardSummaryAsync("tenant-a");
        await sut.GetDashboardSummaryAsync("tenant-a");

        Assert.Equal(1, repository.CallCount);
    }

    [Fact]
    public async Task GetAnalyticsReport_ReturnsKpisByRegionAndChannel()
    {
        var repository = new FakeReportingRepository();
        var sut = new ReportingQueryService(repository, new MemoryCache(new MemoryCacheOptions()));

        await repository.ProcessEventAsync(new ReportingEvent("evt-1", "AlertTriggered", "tenant-a", "north", "High", "sms", 0.82, "Success"));
        await repository.ProcessEventAsync(new ReportingEvent("evt-2", "NotificationSent", "tenant-a", "north", "High", "sms", 0.82, "Success"));
        await repository.ProcessEventAsync(new ReportingEvent("evt-3", "NotificationFailed", "tenant-a", "south", "Medium", "push", 0.51, "Failed"));

        var analytics = await sut.GetAnalyticsReportAsync("tenant-a");

        Assert.Equal(3, analytics.ProcessedEvents);
        Assert.Equal(1, analytics.AlertCount);
        Assert.Equal(1, analytics.SuccessfulDeliveries);
        Assert.Equal(1, analytics.FailedDeliveries);
        Assert.Contains(analytics.RegionBreakdown, item => item.Region == "north" && item.Count == 2);
        Assert.Contains(analytics.RegionBreakdown, item => item.Region == "south" && item.Count == 1);
    }

    [Fact]
    public async Task GetAnalyticsReport_WithSeverityChannelAndStatusFilter_ReturnsMatchingEvents()
    {
        var repository = new FakeReportingRepository();
        var sut = new ReportingQueryService(repository);

        await repository.ProcessEventAsync(new ReportingEvent("evt-1", "AlertTriggered", "tenant-a", "north", "High", "sms", 0.82, "Success"));
        await repository.ProcessEventAsync(new ReportingEvent("evt-2", "NotificationSent", "tenant-a", "north", "Medium", "push", 0.71, "Success"));
        await repository.ProcessEventAsync(new ReportingEvent("evt-3", "NotificationFailed", "tenant-a", "south", "High", "sms", 0.55, "Failed"));

        var analytics = await sut.GetAnalyticsReportAsync(
            "tenant-a",
            new ReportFilter(null, "north", null, "High", "sms", "Success"));

        Assert.Equal(1, analytics.ProcessedEvents);
        Assert.Equal(1, analytics.AlertCount);
        Assert.Equal(0, analytics.FailedDeliveries);
        Assert.Contains(analytics.ChannelBreakdown, item => item.Channel == "sms" && item.Count == 1);
    }

    [Fact]
    public async Task GetAnalyticsReport_ReturnsDeliveryRateAndTrendBreakdown()
    {
        var repository = new FakeReportingRepository();
        var sut = new ReportingQueryService(repository);

        await repository.ProcessEventAsync(new ReportingEvent("evt-1", "NotificationSent", "tenant-a", "north", "High", "sms", 0.82, "Success", new DateTime(2026, 7, 7, 10, 0, 0, DateTimeKind.Utc)));
        await repository.ProcessEventAsync(new ReportingEvent("evt-2", "NotificationFailed", "tenant-a", "south", "Medium", "push", 0.51, "Failed", new DateTime(2026, 7, 7, 12, 0, 0, DateTimeKind.Utc)));
        await repository.ProcessEventAsync(new ReportingEvent("evt-3", "AlertTriggered", "tenant-a", "north", "High", "sms", 0.91, "Success", new DateTime(2026, 7, 8, 8, 0, 0, DateTimeKind.Utc)));

        var analytics = await sut.GetAnalyticsReportAsync("tenant-a");

        Assert.Equal(50, analytics.DeliveryRate, 2);
        Assert.Equal(50, analytics.FailureRate, 2);
        Assert.Equal(0.91, analytics.MaxRiskScore, 2);
        Assert.Contains(analytics.SeverityBreakdown, item => item.Severity == "High" && item.Count == 2);
        Assert.Contains(analytics.TimeSeriesBreakdown, item => item.Period == "2026-07-07" && item.Count == 2);
    }

    private sealed class FakeReportingRepository : IReportingRepository
    {
        private readonly List<ReportingEvent> _events = [];

        public int CallCount { get; private set; }

        public Task ProcessEventAsync(ReportingEvent reportingEvent, CancellationToken cancellationToken = default)
        {
            _events.Add(reportingEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<ReportingEvent>> GetEventsForTenantAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyCollection<ReportingEvent>>(_events
                .Where(e => string.Equals(e.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                .ToArray());
        }
    }
}
