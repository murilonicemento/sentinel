namespace Reporting.Domain.Entities;

public sealed record ReportFilter(
    string? TenantId = null,
    string? Region = null,
    string? EventType = null,
    string? Severity = null,
    string? Channel = null,
    string? Status = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null);
