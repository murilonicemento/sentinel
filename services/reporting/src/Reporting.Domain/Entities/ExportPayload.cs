namespace Reporting.Domain.Entities;

public sealed record ExportPayload(
    string TenantId,
    string Format,
    IReadOnlyCollection<ReportingEvent> Events);
