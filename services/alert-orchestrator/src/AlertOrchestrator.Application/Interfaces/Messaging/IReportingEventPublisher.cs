using AlertOrchestrator.Application.Events;

namespace AlertOrchestrator.Application.Interfaces.Messaging;

/// <summary>Sends reporting data to the reporting service (<c>reporting-events</c>).</summary>
public interface IReportingEventPublisher
{
    Task PublishAsync(ReportingEventEnvelope reportingEvent, CancellationToken cancellationToken = default);
}
