using Reporting.Domain.Entities;

namespace Reporting.Domain.Interfaces;

public interface IReportingAuditRepository
{
    Task SaveAsync(ReportingEventProcessingAudit audit, CancellationToken cancellationToken = default);
}
