using MediatR;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.Interfaces.HttpClients;
using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Interfaces;
using RiskEvaluation.Domain.Repositories;

namespace RiskEvaluation.Application.Handlers;

/// <summary>
/// Records the published catalog version, copies its regional parameters, severity criteria and matrix entries into a
/// local snapshot used by every evaluation, and drops scores cached under the previous version. If risk-catalog cannot
/// answer, the exception reaches the consumer, which retries the publication.
/// </summary>
public class RiskCatalogPublishedEventHandler : INotificationHandler<RiskCatalogPublishedEvent>
{
    private readonly IRecentScoresCache _cache;
    private readonly IRiskCatalogClient _catalogClient;
    private readonly ICatalogSnapshotRepository _catalogSnapshots;
    private readonly ICatalogVersionRepository _catalogVersions;
    private readonly ILogger<RiskCatalogPublishedEventHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public RiskCatalogPublishedEventHandler(
        ICatalogVersionRepository catalogVersions,
        ICatalogSnapshotRepository catalogSnapshots,
        IRiskCatalogClient catalogClient,
        IRecentScoresCache cache,
        ILogger<RiskCatalogPublishedEventHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _catalogVersions = catalogVersions;
        _catalogSnapshots = catalogSnapshots;
        _catalogClient = catalogClient;
        _cache = cache;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task Handle(RiskCatalogPublishedEvent notification, CancellationToken cancellationToken)
    {
        await _catalogVersions.SaveAsync(new CatalogVersion(notification.Version, notification.Notes,
            notification.PublishedAt, notification.PublishedBy, _timeProvider.GetUtcNow().UtcDateTime),
            cancellationToken);

        var snapshot = await _catalogClient.FetchSnapshotAsync(notification.Version, cancellationToken);
        await _catalogSnapshots.SaveAsync(snapshot, cancellationToken);

        var removed = await _cache.InvalidateAllAsync(cancellationToken);

        _logger.LogInformation(
            "Risk catalog version {Version} copied ({Regions} regions, {Criteria} severity criteria, {Matrix} matrix entries); {Removed} cached scores invalidated",
            notification.Version, snapshot.Regions.Count, snapshot.SeverityRules.Count, snapshot.MatrixRules.Count,
            removed);
    }
}
