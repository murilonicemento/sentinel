using Reporting.Domain.Entities;

namespace Reporting.Domain.Interfaces;

public interface IReportingRepository
{
    Task ProcessEventAsync(ReportingEvent reportingEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ReportingEvent>> GetEventsForTenantAsync(string tenantId, CancellationToken cancellationToken = default);
}
