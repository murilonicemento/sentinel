using Reporting.Application.Models;

namespace Reporting.Application.Interfaces;

public interface IReportingReplayPublisher
{
    Task<ReplayPublishResult> PublishAsync(
        string sourceTopic,
        string payload,
        string requestedBy,
        CancellationToken cancellationToken = default);
}
