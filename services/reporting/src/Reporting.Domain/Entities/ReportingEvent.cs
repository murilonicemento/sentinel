namespace Reporting.Domain.Entities;

public sealed class ReportingEvent
{
    public ReportingEvent()
    {
    }

    public ReportingEvent(string eventId, string eventType, string tenantId, string region, string severity, string channel, double riskScore, string status, DateTime? timestamp = null)
    {
        EventId = eventId;
        EventType = eventType;
        TenantId = tenantId;
        Region = region;
        Severity = severity;
        Channel = channel;
        RiskScore = riskScore;
        Status = status;
        Timestamp = timestamp ?? DateTime.UtcNow;
    }

    public string EventId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public double RiskScore { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
