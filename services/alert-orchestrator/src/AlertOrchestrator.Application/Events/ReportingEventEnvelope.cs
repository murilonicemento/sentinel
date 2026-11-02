namespace AlertOrchestrator.Application.Events;

/// <summary>
/// Payload of <c>reporting-events</c>, consumed by reporting (X-24). alert-orchestrator reports one
/// <c>AlertTriggered</c> per triggered alert window.
/// </summary>
public sealed record ReportingEventEnvelope(
    string EventId,
    string EventType,
    string TenantId,
    string Region,
    string Severity,
    string Channel,
    double RiskScore,
    string Status,
    DateTime Timestamp,
    string Source)
{
    /// <summary>
    /// The trigger of a window. <c>EventId</c> derives from the window, so a republished trigger is stored once. The
    /// severity is <c>HIGH</c>, like the trigger command's; the event has no delivery channel. Null without a tenant.
    /// </summary>
    public static ReportingEventEnvelope? ForTriggeredAlert(string? tenantId, Guid alertWindowId, string region,
        double riskScore, DateTime triggeredAt) =>
        string.IsNullOrWhiteSpace(tenantId)
            ? null
            : new ReportingEventEnvelope($"alert-triggered:{alertWindowId:N}", "AlertTriggered", tenantId, region,
                "HIGH", "none", riskScore, "SUCCESS", triggeredAt.ToUniversalTime(), "alert-orchestrator");
}
