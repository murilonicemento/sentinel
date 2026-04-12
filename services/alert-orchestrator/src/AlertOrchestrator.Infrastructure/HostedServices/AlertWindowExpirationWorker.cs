using AlertOrchestrator.Application.Interfaces.Messaging;
using AlertOrchestrator.Application.Interfaces.Observability;
using AlertOrchestrator.Domain.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Infrastructure.HostedServices;

public sealed class AlertWindowExpirationWorker : BackgroundService
{
    private readonly TimeSpan _checkInterval;
    private readonly ILogger<AlertWindowExpirationWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public AlertWindowExpirationWorker(
        IServiceProvider serviceProvider,
        ILogger<AlertWindowExpirationWorker> logger,
        TimeSpan? checkInterval = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _checkInterval = checkInterval ?? TimeSpan.FromMinutes(1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "AlertWindowExpirationWorker started with interval: {Interval}",
            _checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredWindowsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired alert windows");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task ProcessExpiredWindowsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAlertWindowRepository>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var metrics = scope.ServiceProvider.GetRequiredService<IAlertMetrics>();

        var expiredWindows = await repository.GetExpiredOpenWindowsAsync(cancellationToken);

        foreach (var window in expiredWindows)
            try
            {
                window.MarkExpired();
                await repository.UpdateAsync(window, cancellationToken);

                metrics.AlertExpired(window.Region, window.RiskType.ToString());

                foreach (var domainEvent in window.DomainEvents)
                    await eventPublisher.PublishAsync(domainEvent, cancellationToken);
                window.ClearDomainEvents();

                _logger.LogInformation(
                    "AlertWindowExpired: {WindowId} marked as expired. Region: {Region}, RiskType: {RiskType}, SignalCount: {SignalCount}",
                    window.Id, window.Region, window.RiskType, window.Signals.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing expired window {WindowId} for Region: {Region}, RiskType: {RiskType}",
                    window.Id, window.Region, window.RiskType);
            }
    }
}