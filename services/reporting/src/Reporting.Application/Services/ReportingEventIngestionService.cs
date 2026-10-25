using Reporting.Domain.Entities;
using Reporting.Domain.Interfaces;

namespace Reporting.Application.Services;

public sealed class ReportingEventIngestionService
{
    private readonly IReportingRepository _repository;

    public ReportingEventIngestionService(IReportingRepository repository)
    {
        _repository = repository;
    }

    public Task IngestAsync(ReportingEvent reportingEvent, CancellationToken cancellationToken = default)
        => _repository.ProcessEventAsync(reportingEvent, cancellationToken);
}
