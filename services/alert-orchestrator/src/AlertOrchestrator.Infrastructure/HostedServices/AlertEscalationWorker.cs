using AlertOrchestrator.Application.Commands;
using AlertOrchestrator.Application.Interfaces.Messaging;
using AlertOrchestrator.Application.Interfaces.Observability;
using AlertOrchestrator.Application.Ports;
using AlertOrchestrator.Domain.Enums;
using AlertOrchestrator.Domain.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Infrastructure.HostedServices;

public sealed class AlertEscalationWorker : BackgroundService
{
    private readonly TimeSpan _checkInterval;
    private readonly ILogger<AlertEscalationWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public AlertEscalationWorker(
        IServiceProvider serviceProvider,
        ILogger<AlertEscalationWorker> logger,
        TimeSpan? checkInterval = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _checkInterval = checkInterval ?? TimeSpan.FromMinutes(1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "AlertEscalationWorker started with interval: {Interval}",
            _checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessEscalationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing alert escalations");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task ProcessEscalationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAlertWindowRepository>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var commandPublisher = scope.ServiceProvider.GetRequiredService<ICommandPublisher>();
        var configurationPort = scope.ServiceProvider.GetRequiredService<IAlertConfigurationPort>();
        var metrics = scope.ServiceProvider.GetRequiredService<IAlertMetrics>();

        var triggeredWindows = await repository.GetTriggeredUnconfirmedWindowsAsync(cancellationToken);

        foreach (var window in triggeredWindows)
            try
            {
                var config = await configurationPort.GetConfigurationAsync(
                    window.RiskType.ToString(),
                    window.TenantId,
                    cancellationToken);

                if (config.EscalationIntervals is null ||
                    !config.EscalationIntervals.Any()) continue; // No escalation configured

                var elapsed = DateTime.UtcNow - window.TriggeredAt!.Value;
                var nextLevel =
                    GetNextEscalationLevel(window.CurrentEscalationLevel, config.EscalationIntervals, elapsed);

                if (!nextLevel.HasValue || !window.Escalate(nextLevel.Value)) continue;
                
                await repository.UpdateAsync(window, cancellationToken);

                metrics.AlertEscalated(window.Region, window.RiskType.ToString(), nextLevel.Value);
                _logger.LogInformation(
                    "AlertEscalated: Window {WindowId} escalated to {Level}. Region: {Region}, RiskType: {RiskType}, Elapsed: {Elapsed}",
                    window.Id, nextLevel.Value, window.Region, window.RiskType, elapsed);

                foreach (var domainEvent in window.DomainEvents)
                    await eventPublisher.PublishAsync(domainEvent, cancellationToken);
                window.ClearDomainEvents();

                // Send escalated alert command
                var command = new TriggerAlertCommand(
                    window.Id,
                    window.Region,
                    window.RiskType.ToString(),
                    window.FinalRiskScore!.Value,
                    window.Signals.Count,
                    window.Signals.Select(s => s.Source.ToString()).ToList(),
                    DateTime.UtcNow,
                    nextLevel.Value);

                await commandPublisher.PublishAsync(command, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing escalation for window {WindowId} in Region: {Region}, RiskType: {RiskType}",
                    window.Id, window.Region, window.RiskType);
            }
    }

    private static AlertEscalationLevel? GetNextEscalationLevel(
        AlertEscalationLevel currentLevel,
        List<TimeSpan> intervals,
        TimeSpan elapsed)
    {
        var levelIndex = (int)currentLevel - 1;
        if (levelIndex >= intervals.Count) return null; // Max level reached

        var nextInterval = intervals[levelIndex];
        if (elapsed >= nextInterval) return (AlertEscalationLevel)(levelIndex + 2); // Next level

        return null;
    }
}