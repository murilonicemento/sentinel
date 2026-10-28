using MediatR;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Interfaces;
using RiskEvaluation.Domain.Repositories;

namespace RiskEvaluation.Application.Handlers;

/// <summary>
/// Records the published catalog version and drops scores cached under the previous one. The evaluation weights come
/// from the RiskFactors collection; using catalog data (matrices, IDF curves, regional parameters) in the score is a
/// separate feature (RE-13 in docs/compliance/services-validation.md).
/// </summary>
public class RiskCatalogPublishedEventHandler : INotificationHandler<RiskCatalogPublishedEvent>
{
    private readonly IRecentScoresCache _cache;
    private readonly ICatalogVersionRepository _catalogVersions;
    private readonly ILogger<RiskCatalogPublishedEventHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public RiskCatalogPublishedEventHandler(
        ICatalogVersionRepository catalogVersions,
        IRecentScoresCache cache,
        ILogger<RiskCatalogPublishedEventHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _catalogVersions = catalogVersions;
        _cache = cache;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task Handle(RiskCatalogPublishedEvent notification, CancellationToken cancellationToken)
    {
        await _catalogVersions.SaveAsync(new CatalogVersion(notification.Version, notification.Notes,
            notification.PublishedAt, notification.PublishedBy, _timeProvider.GetUtcNow().UtcDateTime),
            cancellationToken);

        var removed = await _cache.InvalidateAllAsync(cancellationToken);

        _logger.LogInformation("Risk catalog version {Version} recorded; {Removed} cached scores invalidated",
            notification.Version, removed);
    }
}
