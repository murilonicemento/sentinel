using ChannelsService.Application.DTOs;

namespace ChannelsService.Application.Interfaces;

/// <summary>Sends reporting data to the reporting service (<c>reporting-events</c>).</summary>
public interface IReportingEventPublisher
{
    Task PublishAsync(ReportingEventEnvelopeDTO reportingEvent, CancellationToken cancellationToken);
}
