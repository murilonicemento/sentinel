using System.Diagnostics;
using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Events;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Application.Services;

public sealed class ChannelDeliveryService : IChannelDeliveryService
{
    private readonly IEnumerable<IChannelProvider> _providers;
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ITenantChannelSettingsProvider _settingsProvider;
    private readonly IRetryPolicyEngine _retryPolicyEngine;
    private readonly IFallbackExecutor _fallbackExecutor;
    private readonly IChannelMetrics _metrics;
    private readonly ILogger<ChannelDeliveryService> _logger;

    public ChannelDeliveryService(
        IEnumerable<IChannelProvider> providers,
        IDeliveryRepository deliveryRepository,
        ITenantChannelSettingsProvider settingsProvider,
        IRetryPolicyEngine retryPolicyEngine,
        IFallbackExecutor fallbackExecutor,
        IChannelMetrics metrics,
        ILogger<ChannelDeliveryService> logger)
    {
        _providers = providers;
        _deliveryRepository = deliveryRepository;
        _settingsProvider = settingsProvider;
        _retryPolicyEngine = retryPolicyEngine;
        _fallbackExecutor = fallbackExecutor;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<DeliveryResultDTO> DeliverAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        var attemptedChannels = 0;
        var settings = _settingsProvider.GetSettings(notification.TenantId);
        var orderedChannels = _fallbackExecutor.GetFallbackOrder(notification, settings);

        _logger.LogInformation(
            "Starting delivery for event {EventId} type {EventType} correlation {CorrelationId} on tenant {TenantId}. Channels: {Channels}",
            notification.EventId,
            notification.EventType,
            notification.CorrelationId,
            notification.TenantId,
            string.Join(',', orderedChannels));

        if (!orderedChannels.Any())
        {
            return new DeliveryResultDTO
            {
                Success = false,
                Error = "No channels are configured for delivery."
            };
        }

        var successfulProviders = new List<string>();
        var failedChannels = new List<string>();

        foreach (var channel in orderedChannels)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new DeliveryResultDTO { Success = false, Error = "Delivery cancelled." };
            }

            var provider = _providers.FirstOrDefault(p => p.ChannelTypeEnum == channel);
            if (provider == null)
            {
                _logger.LogWarning("No provider registered for channel {Channel}.", channel);
                failedChannels.Add(channel.ToString());
                continue;
            }

            var maxRetries = settings.MaxRetries.GetValueOrDefault(channel, 3);
            var attemptNumber = 0;
            var resilienceOptions = (provider as IResilientChannelProvider)?.ResilienceOptions ?? new ProviderResilienceDTO();
            attemptedChannels++;
            var attemptStopwatch = Stopwatch.StartNew();

            var attemptResult = await _retryPolicyEngine.ExecuteAsync(async ct =>
            {
                attemptNumber++;
                var deliveryResult = await provider.SendAsync(notification, ct);
                deliveryResult.ProviderName ??= provider.ProviderName;

                var status = deliveryResult.Success
                    ? attemptNumber > 1 ? DeliveryStatusEnum.Retried : DeliveryStatusEnum.Sent
                    : DeliveryStatusEnum.Failed;

                await _deliveryRepository.AddAsync(new DeliveryAttempt
                {
                    AttemptId = Guid.NewGuid().ToString("N"),
                    EventId = notification.EventId,
                    TenantId = notification.TenantId,
                    Channel = channel,
                    Provider = provider.ProviderName,
                    StatusEnum = status,
                    AttemptCount = attemptNumber,
                    ErrorMessage = deliveryResult.Error,
                    Timestamp = DateTime.UtcNow
                });

                return deliveryResult;
            }, maxRetries, resilienceOptions, cancellationToken);

            attemptStopwatch.Stop();
            _metrics.RecordChannelAttempt(channel, provider.ProviderName, attemptResult.Success, attemptNumber > 1, attemptNumber);
            _metrics.RecordChannelLatency(channel, provider.ProviderName, attemptStopwatch.Elapsed.TotalSeconds, attemptResult.Success ? "success" : "failure");

            if (attemptResult.Success)
            {
                successfulProviders.Add(provider.ProviderName);
                _logger.LogInformation(
                    "Delivery succeeded for event {EventId} on channel {Channel} using provider {Provider}.",
                    notification.EventId,
                    channel,
                    attemptResult.ProviderName);
            }
            else
            {
                _logger.LogWarning(
                    "Channel {Channel} failed for event {EventId} after {Attempts} attempts: {Error}",
                    channel,
                    notification.EventId,
                    maxRetries,
                    attemptResult.Error);
                failedChannels.Add(channel.ToString());
            }
        }

        if (successfulProviders.Any())
        {
            return new DeliveryResultDTO
            {
                Success = true,
                ProviderName = string.Join(",", successfulProviders),
                Error = failedChannels.Any() ? $"Some channels failed: {string.Join(",", failedChannels)}" : null
            };
        }

        return new DeliveryResultDTO
        {
            Success = false,
            Error = failedChannels.Any()
                ? $"All configured channels failed: {string.Join(",", failedChannels)}"
                : "No channels were attempted."
        };
    }
}