using Reporting.Domain.Entities;

namespace Reporting.Application.Interfaces;

public interface IReportingEventConsumer
{
    Task ProcessAsync(ReportingEventEnvelope envelope, CancellationToken cancellationToken = default);
}
