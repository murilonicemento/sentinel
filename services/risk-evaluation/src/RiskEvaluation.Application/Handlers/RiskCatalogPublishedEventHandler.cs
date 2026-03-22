using MediatR;
using Microsoft.Extensions.Logging;

namespace RiskEvaluation.Application.Handlers;

public class RiskCatalogPublishedEventHandler : INotificationHandler<RiskCatalogPublishedEvent>
{
    private readonly ILogger<RiskCatalogPublishedEventHandler> _logger;

    public RiskCatalogPublishedEventHandler(ILogger<RiskCatalogPublishedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(RiskCatalogPublishedEvent notification, CancellationToken cancellationToken)
    {
        // Update risk model if needed
        _logger.LogInformation("Risk catalog published event received. Model version: {Version}", notification.Version);
        return Task.CompletedTask;
    }
}