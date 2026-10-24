using Reporting.Domain.Entities;

namespace Reporting.Application.Interfaces;

public interface IReportingService
{
    Task<DashboardSummary> GetDashboardSummaryAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<AnalyticsReport> GetAnalyticsReportAsync(string tenantId, ReportFilter? filter = null, CancellationToken cancellationToken = default);
    Task<string> ExportAsync(string tenantId, ReportFilter? filter = null, string format = "csv", CancellationToken cancellationToken = default);
}
