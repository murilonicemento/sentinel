using AlertOrchestrator.Application.Commands;
using AlertOrchestrator.Application.Events;
using AlertOrchestrator.Application.Interfaces.Messaging;
using AlertOrchestrator.Application.Interfaces.Observability;
using AlertOrchestrator.Application.Ports;
using AlertOrchestrator.Domain.Aggregates;
using AlertOrchestrator.Domain.Configuration;
using AlertOrchestrator.Domain.Enums;
using AlertOrchestrator.Domain.Interfaces.Repositories;
using AlertOrchestrator.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Application.Handlers;

public sealed class RegionIntersectedEventHandler : INotificationHandler<RegionIntersectedEvent>
{
    private readonly ICommandPublisher _commandPublisher;
    private readonly IAlertConfigurationPort _configurationPort;
    private readonly IEventPublisher _eventPublisher;
    private readonly IIdempotencyService _idempotencyService;
    private readonly ILogger<RegionIntersectedEventHandler> _logger;
    private readonly IAlertMetrics _metrics;
    private readonly IAlertWindowRepository _windowRepository;

    public RegionIntersectedEventHandler(
        IAlertWindowRepository windowRepository,
        IEventPublisher eventPublisher,
        ICommandPublisher commandPublisher,
        IIdempotencyService idempotencyService,
        IAlertConfigurationPort configurationPort,
        IAlertMetrics metrics,
        ILogger<RegionIntersectedEventHandler> logger)
    {
        _windowRepository = windowRepository;
        _eventPublisher = eventPublisher;
        _commandPublisher = commandPublisher;
        _idempotencyService = idempotencyService;
        _configurationPort = configurationPort;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task Handle(RegionIntersectedEvent notification, CancellationToken cancellationToken)
    {
        if (await _idempotencyService.IsProcessedAsync(notification.EventId, cancellationToken))
        {
            _logger.LogInformation("Event {EventId} already processed, skipping", notification.EventId);
            return;
        }

        try
        {
            var config = await _configurationPort.GetConfigurationAsync(notification.RiskType, null, cancellationToken);

            var window = await _windowRepository.GetOpenWindowAsync(
                notification.Region,
                RiskType.FromString(notification.RiskType),
                null,
                cancellationToken);

            if (window is null)
            {
                window = AlertWindow.Open(
                    notification.Region,
                    RiskType.FromString(notification.RiskType),
                    null,
                    config.Threshold,
                    config.WindowDuration);

                await _windowRepository.AddAsync(window, cancellationToken);

                _logger.LogInformation(
                    "AlertWindowOpened: {WindowId} for region intersection {Region}/{IntersectingRegion}",
                    window.Id, notification.Region, notification.IntersectingRegion);

                _metrics.AlertWindowOpened(notification.Region, notification.RiskType);

                foreach (var domainEvent in window.DomainEvents)
                    await _eventPublisher.PublishAsync(domainEvent, cancellationToken);
                window.ClearDomainEvents();
            }

            var signal = new Signal(
                SignalSource.ExternalApi,
                notification.Timestamp,
                notification.EventId,
                notification.Severity,
                RiskType.FromString(notification.RiskType),
                new Dictionary<string, string>
                {
                    ["intersectingRegion"] = notification.IntersectingRegion,
                    ["eventType"] = "RegionIntersection"
                });

            window.AddSignal(signal);
            _metrics.SignalAdded(notification.Region, notification.RiskType, signal.Source.ToString());

            _logger.LogDebug(
                "SignalAdded: Region intersection event {EventId} added to window {WindowId}",
                notification.EventId, window.Id);

            var quorumConfig = new QuorumConfiguration(
                config.MinimumQuorumSignals,
                config.RequiredDistinctSources,
                config.RequireSensor,
                config.RequireSatellite);

            if (window.TryTrigger(notification.Severity, quorumConfig))
            {
                await _windowRepository.UpdateAsync(window, cancellationToken);

                _metrics.AlertTriggered(notification.Region, notification.RiskType, window.Signals.Count);
                _logger.LogInformation(
                    "AlertTriggered: Region intersection window {WindowId} triggered. Severity: {Severity}, Signals: {SignalCount}, Region: {Region}, IntersectingRegion: {IntersectingRegion}",
                    window.Id, notification.Severity, window.Signals.Count, notification.Region,
                    notification.IntersectingRegion);

                foreach (var domainEvent in window.DomainEvents)
                    await _eventPublisher.PublishAsync(domainEvent, cancellationToken);
                window.ClearDomainEvents();

                var command = new TriggerAlertCommand(
                    window.Id,
                    window.Region,
                    window.RiskType.ToString(),
                    notification.Severity,
                    window.Signals.Count,
                    window.Signals.Select(s => s.Source.ToString()).ToList(),
                    DateTime.UtcNow,
                    window.CurrentEscalationLevel);

                await _commandPublisher.PublishAsync(command, cancellationToken);
            }
            else
            {
                await _windowRepository.UpdateAsync(window, cancellationToken);

                var quorumMet = window.MeetsQuorum(quorumConfig);
                if (!quorumMet)
                    _metrics.QuorumFailed(
                        notification.Region,
                        notification.RiskType,
                        window.Signals.Count,
                        quorumConfig.MinimumSignals);

                _logger.LogDebug(
                    "WindowUpdated: {WindowId} region intersection signal added. Signals: {SignalCount}, QuorumMet: {QuorumMet}",
                    window.Id, window.Signals.Count, quorumMet);
            }

            await _idempotencyService.MarkAsProcessedAsync(notification.EventId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing RegionIntersectedEvent {EventId} for Region: {Region}, IntersectingRegion: {IntersectingRegion}, RiskType: {RiskType}",
                notification.EventId, notification.Region, notification.IntersectingRegion, notification.RiskType);
            throw;
        }
    }
}