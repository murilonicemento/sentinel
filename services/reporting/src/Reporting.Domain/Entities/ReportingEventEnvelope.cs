namespace Reporting.Domain.Entities;

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
    string Source);
