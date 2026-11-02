using AlertOrchestrator.Application.Commands;
using AlertOrchestrator.Application.DTOs;
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

public sealed class RiskUpdatedEventHandler : INotificationHandler<RiskUpdatedEvent>
{
    private readonly ICommandPublisher _commandPublisher;
    private readonly IAlertConfigurationPort _configurationPort;
    private readonly IEventPublisher _eventPublisher;
    private readonly IIdempotencyService _idempotencyService;
    private readonly ILogger<RiskUpdatedEventHandler> _logger;
    private readonly IAlertMetrics _metrics;
    private readonly IAlertWindowRepository _windowRepository;

    public RiskUpdatedEventHandler(
        IAlertWindowRepository windowRepository,
        IEventPublisher eventPublisher,
        ICommandPublisher commandPublisher,
        IIdempotencyService idempotencyService,
        IAlertConfigurationPort configurationPort,
        IAlertMetrics metrics,
        ILogger<RiskUpdatedEventHandler> logger)
    {
        _windowRepository = windowRepository;
        _eventPublisher = eventPublisher;
        _commandPublisher = commandPublisher;
        _idempotencyService = idempotencyService;
        _configurationPort = configurationPort;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task Handle(RiskUpdatedEvent notification, CancellationToken cancellationToken)
    {
        if (await _idempotencyService.IsProcessedAsync(notification.EventId, cancellationToken))
        {
            _logger.LogInformation("Event {EventId} already processed, skipping", notification.EventId);
            return;
        }

        try
        {
            var config =
                await _configurationPort.GetConfigurationAsync(notification.RiskType, notification.TenantId,
                    cancellationToken);

            if (!ShouldEvaluate(config, notification.RiskScore))
            {
                _logger.LogDebug("Risk score {Score} below threshold {Threshold} for {RiskType}",
                    notification.RiskScore, config.Threshold, notification.RiskType);
                await _idempotencyService.MarkAsProcessedAsync(notification.EventId, cancellationToken);
                return;
            }

            var window = await _windowRepository.GetOpenWindowAsync(
                notification.Region,
                RiskType.FromString(notification.RiskType),
                notification.TenantId,
                cancellationToken);

            if (window is null)
            {
                window = AlertWindow.Open(
                    notification.Region,
                    RiskType.FromString(notification.RiskType),
                    notification.TenantId,
                    config.Threshold,
                    config.WindowDuration);

                await _windowRepository.AddAsync(window, cancellationToken);

                _logger.LogInformation(
                    "AlertWindowOpened: {WindowId} for {Region}/{RiskType} with threshold {Threshold}",
                    window.Id, notification.Region, notification.RiskType, config.Threshold);

                _metrics.AlertWindowOpened(notification.Region, notification.RiskType);

                foreach (var domainEvent in window.DomainEvents)
                    await _eventPublisher.PublishAsync(domainEvent, cancellationToken);
                window.ClearDomainEvents();
            }

            var signal = new Signal(
                MapSource(notification.Metadata.GetValueOrDefault("source", "SENSOR")),
                notification.Timestamp,
                notification.EventId,
                notification.RiskScore,
                RiskType.FromString(notification.RiskType),
                notification.Metadata);

            window.AddSignal(signal);
            _metrics.SignalAdded(notification.Region, notification.RiskType, signal.Source.ToString());

            _logger.LogDebug(
                "SignalAdded: Event {EventId} added to window {WindowId} from source {Source}",
                notification.EventId, window.Id, signal.Source);

            var quorumConfig = new QuorumConfiguration(
                config.MinimumQuorumSignals,
                config.RequiredDistinctSources,
                config.RequireSensor,
                config.RequireSatellite);

            if (window.TryTrigger(notification.RiskScore, quorumConfig))
            {
                await _windowRepository.UpdateAsync(window, cancellationToken);

                _metrics.AlertTriggered(notification.Region, notification.RiskType, window.Signals.Count);
                _logger.LogInformation(
                    "AlertTriggered: Window {WindowId} triggered with {SignalCount} signals. Score: {Score}, Region: {Region}, RiskType: {RiskType}",
                    window.Id, window.Signals.Count, notification.RiskScore, notification.Region,
                    notification.RiskType);

                foreach (var domainEvent in window.DomainEvents)
                    await _eventPublisher.PublishAsync(domainEvent, cancellationToken);
                window.ClearDomainEvents();

                var command = new TriggerAlertCommand(
                    window.Id,
                    window.Region,
                    window.RiskType.ToString(),
                    notification.RiskScore,
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
                    "WindowUpdated: {WindowId} signals: {SignalCount}, QuorumMet: {QuorumMet}, ShouldTrigger: {ShouldTrigger}",
                    window.Id, window.Signals.Count, quorumMet, window.ShouldTrigger(notification.RiskScore));
            }

            await _idempotencyService.MarkAsProcessedAsync(notification.EventId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing RiskUpdatedEvent {EventId} for Region: {Region}, RiskType: {RiskType}",
                notification.EventId, notification.Region, notification.RiskType);
            throw;
        }
    }

    private static bool ShouldEvaluate(TriggerRuleConfiguration config, double riskScore)
    {
        return riskScore >= config.Threshold * 0.8;
    }

    private static SignalSource MapSource(string source)
    {
        return source.ToUpperInvariant() switch
        {
            "SENSOR" => SignalSource.Sensor,
            "SATELLITE" => SignalSource.Satellite,
            "ML" or "MACHINELEARNING" => SignalSource.MachineLearning,
            "API" or "EXTERNAL" => SignalSource.ExternalApi,
            _ => SignalSource.Sensor
        };
    }
}