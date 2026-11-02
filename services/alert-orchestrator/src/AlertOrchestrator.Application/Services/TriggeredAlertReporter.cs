using AlertOrchestrator.Application.Events;
using AlertOrchestrator.Application.Interfaces.Messaging;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Application.Services;

/// <summary>
/// Tells tenants-billing and reporting that a window triggered (ADR-0009, X-24): one alert of usage and one
/// <c>AlertTriggered</c>. Escalations are not reported again. Both ids derive from the window, so a republished
/// trigger is counted once.
/// </summary>
public sealed class TriggeredAlertReporter
{
    private readonly ITenantUsagePublisher _usagePublisher;
    private readonly IReportingEventPublisher _reportingPublisher;
    private readonly ILogger<TriggeredAlertReporter> _logger;

    public TriggeredAlertReporter(ITenantUsagePublisher usagePublisher, IReportingEventPublisher reportingPublisher,
        ILogger<TriggeredAlertReporter> logger)
    {
        _usagePublisher = usagePublisher;
        _reportingPublisher = reportingPublisher;
        _logger = logger;
    }

    public async Task ReportAsync(string? tenantId, Guid alertWindowId, string region, double riskScore,
        DateTime triggeredAt, CancellationToken cancellationToken)
    {
        var usage = TenantUsageEvent.ForTriggeredAlert(tenantId, alertWindowId);
        if (usage is null)
            _logger.LogWarning("Window {WindowId} has no tenant id in GUID form; its triggered alert is not reported " +
                               "as usage", alertWindowId);
        else
            await _usagePublisher.PublishAsync(usage, cancellationToken);

        var reportingEvent = ReportingEventEnvelope.ForTriggeredAlert(tenantId, alertWindowId, region, riskScore,
            triggeredAt);
        if (reportingEvent is null)
            _logger.LogWarning("Window {WindowId} has no tenant; its triggered alert is not sent to reporting",
                alertWindowId);
        else
            await _reportingPublisher.PublishAsync(reportingEvent, cancellationToken);
    }
}
