using MediatR;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Domain.Contracts;
using RiskEvaluation.Domain.Entities;

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
        if (notification == null)
        {
            _logger.LogError("RiskCatalogPublishedEvent received but notification is null");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(notification.Version))
        {
            _logger.LogWarning("Risk catalog published event rejected: Version is required");
            return Task.CompletedTask;
        }

        try
        {
            var riskModel = new RiskModel(
                notification.Version,
                notification.Parameters ?? new Dictionary<string, double>(),
                notification.Formula ?? string.Empty);

            _logger.LogInformation(
                "Risk catalog published successfully. Version: {Version}, Parameters: {ParameterCount}, Formula: {Formula}",
                riskModel.Version,
                riskModel.Parameters.Count,
                riskModel.Formula);

            if (riskModel.Parameters.Any())
            {
                _logger.LogDebug("Catalog parameters: {@Parameters}", riskModel.Parameters);
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Failed to create RiskModel from catalog event. Version: {Version}", notification.Version);
        }

        return Task.CompletedTask;
    }
}