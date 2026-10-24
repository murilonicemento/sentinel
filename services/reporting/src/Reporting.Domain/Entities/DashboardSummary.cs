namespace Reporting.Domain.Entities;

public sealed record DashboardSummary(
    string TenantId,
    int ProcessedEvents,
    int TotalAlerts,
    int SuccessfulDeliveries,
    double AverageRiskScore);
