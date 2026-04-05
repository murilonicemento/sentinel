using AlertOrchestrator.Application.Commands;
using AlertOrchestrator.Application.Interfaces.Messaging;
using AlertOrchestrator.Application.Interfaces.Observability;
using AlertOrchestrator.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Application.Handlers;

public sealed class ConfirmAlertReceiptCommandHandler : IRequestHandler<ConfirmAlertReceiptCommand>
{
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<ConfirmAlertReceiptCommandHandler> _logger;
    private readonly IAlertMetrics _metrics;
    private readonly IAlertWindowRepository _windowRepository;

    public ConfirmAlertReceiptCommandHandler(
        IAlertWindowRepository windowRepository,
        IEventPublisher eventPublisher,
        IAlertMetrics metrics,
        ILogger<ConfirmAlertReceiptCommandHandler> logger)
    {
        _windowRepository = windowRepository;
        _eventPublisher = eventPublisher;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task Handle(ConfirmAlertReceiptCommand request, CancellationToken cancellationToken)
    {
        var window = await _windowRepository.GetByIdAsync(request.AlertWindowId, cancellationToken);
        if (window is null)
        {
            _logger.LogWarning("Alert window {WindowId} not found for confirmation", request.AlertWindowId);
            return;
        }

        window.ConfirmReceipt();
        await _windowRepository.UpdateAsync(window, cancellationToken);

        _metrics.AlertConfirmed(window.Region, window.RiskType.ToString());

        foreach (var domainEvent in window.DomainEvents)
            await _eventPublisher.PublishAsync(domainEvent, cancellationToken);
        window.ClearDomainEvents();

        _logger.LogInformation(
            "AlertConfirmed: Window {WindowId} confirmed by {ConfirmedBy}. Region: {Region}, RiskType: {RiskType}",
            window.Id, request.ConfirmedBy, window.Region, window.RiskType);
    }
}