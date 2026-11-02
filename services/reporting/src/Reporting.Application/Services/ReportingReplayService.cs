using Reporting.Application.Interfaces;
using Reporting.Application.Models;

namespace Reporting.Application.Services;

public sealed class ReportingReplayService
{
    private const string DefaultRequester = "admin";

    private readonly IReportingReplayPublisher _replayPublisher;

    public ReportingReplayService(IReportingReplayPublisher replayPublisher)
    {
        _replayPublisher = replayPublisher;
    }

    public Task<ReplayPublishResult> RequestReplayAsync(
        string sourceTopic,
        string payload,
        string? requestedBy,
        CancellationToken cancellationToken = default)
    {
        var requester = string.IsNullOrWhiteSpace(requestedBy) ? DefaultRequester : requestedBy;

        return _replayPublisher.PublishAsync(sourceTopic, payload, requester, cancellationToken);
    }
}
