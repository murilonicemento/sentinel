using MediatR;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Domain.Contracts;
using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Repositories;

namespace RiskEvaluation.Application.Handlers;

public class RiskCatalogPublishedEventHandler : INotificationHandler<RiskCatalogPublishedEvent>
{
    private readonly IRiskModelRepository _riskModelRepository;
    private readonly ILogger<RiskCatalogPublishedEventHandler> _logger;

    public RiskCatalogPublishedEventHandler(
        IRiskModelRepository riskModelRepository,
        ILogger<RiskCatalogPublishedEventHandler> logger)
    {
        _riskModelRepository = riskModelRepository;
        _logger = logger;
    }

    public async Task Handle(RiskCatalogPublishedEvent notification, CancellationToken cancellationToken)
    {
        if (notification == null)
        {
            _logger.LogError("RiskCatalogPublishedEvent received but notification is null");
            return;
        }

        if (string.IsNullOrWhiteSpace(notification.Version))
        {
            _logger.LogWarning("Risk catalog published event rejected: Version is required");
            return;
        }

        try
        {
            var riskModel = new RiskModel(
                notification.Version,
                notification.Parameters ?? new Dictionary<string, double>(),
                notification.Formula ?? string.Empty);

            await _riskModelRepository.SaveAsync(riskModel, cancellationToken);

            _logger.LogInformation(
                "Risk catalog published and saved. Version: {Version}, Parameters: {ParameterCount}, Formula: {Formula}",
                riskModel.Version,
                riskModel.Parameters.Count,
                riskModel.Formula);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Failed to create RiskModel from catalog event. Version: {Version}", notification.Version);
        }
    }
}