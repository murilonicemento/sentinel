using Microsoft.Extensions.Caching.Memory;
using Reporting.Application.Interfaces;
using Reporting.Domain.Entities;
using Reporting.Domain.Interfaces;

namespace Reporting.Application.Services;

public class ReportingQueryService : IReportingService
{
    private readonly IReportingRepository _repository;
    private readonly IMemoryCache _cache;

    public ReportingQueryService(IReportingRepository repository, IMemoryCache? cache = null)
    {
        _repository = repository;
        _cache = cache ?? new MemoryCache(new MemoryCacheOptions());
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var cacheKey = $"summary:{tenantId}";
        if (_cache.TryGetValue(cacheKey, out DashboardSummary? cachedSummary))
        {
            return cachedSummary!;
        }

        var events = await GetFilteredEventsAsync(tenantId, null, cancellationToken);
        var processedEvents = events.Count;
        var totalAlerts = events.Count(e => string.Equals(e.EventType, "AlertTriggered", StringComparison.OrdinalIgnoreCase));
        var successfulDeliveries = events.Count(e =>
            string.Equals(e.EventType, "NotificationSent", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Status, "Success", StringComparison.OrdinalIgnoreCase));
        var averageRiskScore = processedEvents == 0
            ? 0d
            : events.Average(e => e.RiskScore);

        var summary = new DashboardSummary(tenantId, processedEvents, totalAlerts, successfulDeliveries, averageRiskScore);
        _cache.Set(cacheKey, summary, TimeSpan.FromMinutes(5));
        return summary;
    }

    public async Task<AnalyticsReport> GetAnalyticsReportAsync(string tenantId, ReportFilter? filter = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var events = await GetFilteredEventsAsync(tenantId, filter, cancellationToken);
        var alertCount = events.Count(e => string.Equals(e.EventType, "AlertTriggered", StringComparison.OrdinalIgnoreCase));
        var successfulDeliveries = events.Count(e =>
            string.Equals(e.EventType, "NotificationSent", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Status, "Success", StringComparison.OrdinalIgnoreCase));
        var failedDeliveries = events.Count(e =>
            string.Equals(e.EventType, "NotificationFailed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.Status, "Failed", StringComparison.OrdinalIgnoreCase));

        var totalDeliveryAttempts = successfulDeliveries + failedDeliveries;
        var deliveryRate = totalDeliveryAttempts == 0 ? 0d : successfulDeliveries * 100d / totalDeliveryAttempts;
        var failureRate = totalDeliveryAttempts == 0 ? 0d : failedDeliveries * 100d / totalDeliveryAttempts;

        var regionBreakdown = events
            .GroupBy(e => e.Region)
            .Select(g => new RegionMetric(g.Key, g.Count()))
            .OrderByDescending(m => m.Count)
            .ToArray();

        var channelBreakdown = events
            .GroupBy(e => e.Channel)
            .Select(g => new ChannelMetric(g.Key, g.Count()))
            .OrderByDescending(m => m.Count)
            .ToArray();

        var severityBreakdown = events
            .GroupBy(e => e.Severity)
            .Select(g => new SeverityMetric(g.Key, g.Count()))
            .OrderByDescending(m => m.Count)
            .ToArray();

        var timeSeriesBreakdown = events
            .GroupBy(e => e.Timestamp.Date.ToString("yyyy-MM-dd"))
            .Select(g => new TimeSeriesMetric(g.Key, g.Count()))
            .OrderBy(m => m.Period)
            .ToArray();

        var averageRiskScore = events.Count == 0 ? 0d : events.Average(e => e.RiskScore);
        var maxRiskScore = events.Count == 0 ? 0d : events.Max(e => e.RiskScore);

        return new AnalyticsReport(
            tenantId,
            events.Count,
            alertCount,
            successfulDeliveries,
            failedDeliveries,
            regionBreakdown,
            channelBreakdown,
            averageRiskScore,
            deliveryRate,
            failureRate,
            maxRiskScore,
            severityBreakdown,
            timeSeriesBreakdown);
    }

    public async Task<string> ExportAsync(string tenantId, ReportFilter? filter = null, string format = "csv", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var events = await GetFilteredEventsAsync(tenantId, filter, cancellationToken);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var payload = new
            {
                TenantId = tenantId,
                Filter = filter,
                Events = events
            };

            return System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }

        return BuildCsv(events);
    }

    private async Task<IReadOnlyCollection<ReportingEvent>> GetFilteredEventsAsync(string tenantId, ReportFilter? filter, CancellationToken cancellationToken)
    {
        var events = await _repository.GetEventsForTenantAsync(tenantId, cancellationToken);

        return events
            .Where(e => filter is null || MatchesFilter(e, filter))
            .ToArray();
    }

    private static string BuildCsv(IEnumerable<ReportingEvent> events)
    {
        var csvLines = new List<string>
        {
            "EventId,EventType,TenantId,Region,Severity,Channel,RiskScore,Status,Timestamp"
        };

        foreach (var evt in events)
        {
            csvLines.Add($"{evt.EventId},{evt.EventType},{evt.TenantId},{evt.Region},{evt.Severity},{evt.Channel},{evt.RiskScore},{evt.Status},{evt.Timestamp:O}");
        }

        return string.Join(System.Environment.NewLine, csvLines);
    }

    private static bool MatchesFilter(ReportingEvent reportingEvent, ReportFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Region) && !string.Equals(reportingEvent.Region, filter.Region, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.EventType) && !string.Equals(reportingEvent.EventType, filter.EventType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Severity) && !string.Equals(reportingEvent.Severity, filter.Severity, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Channel) && !string.Equals(reportingEvent.Channel, filter.Channel, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) && !string.Equals(reportingEvent.Status, filter.Status, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.StartDate is not null && reportingEvent.Timestamp < filter.StartDate.Value)
        {
            return false;
        }

        if (filter.EndDate is not null && reportingEvent.Timestamp > filter.EndDate.Value)
        {
            return false;
        }

        return true;
    }
}
