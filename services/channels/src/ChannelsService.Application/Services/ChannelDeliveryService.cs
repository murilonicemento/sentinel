using System.Diagnostics;
using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Application.Interfaces.Services;
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
    private readonly ITenantBillingGateway _tenantBillingGateway;

    public ChannelDeliveryService(
        IEnumerable<IChannelProvider> providers,
        IDeliveryRepository deliveryRepository,
        ITenantChannelSettingsProvider settingsProvider,
        IRetryPolicyEngine retryPolicyEngine,
        IFallbackExecutor fallbackExecutor,
        IChannelMetrics metrics,
        ILogger<ChannelDeliveryService> logger,
        ITenantBillingGateway tenantBillingGateway)
    {
        _providers = providers;
        _deliveryRepository = deliveryRepository;
        _settingsProvider = settingsProvider;
        _retryPolicyEngine = retryPolicyEngine;
        _fallbackExecutor = fallbackExecutor;
        _metrics = metrics;
        _logger = logger;
        _tenantBillingGateway = tenantBillingGateway;
    }

    public async Task<DeliveryResultDTO> DeliverAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        // Validate tenant is active and authorized
        if (!string.IsNullOrWhiteSpace(notification.TenantId))
        {
            var isTenantActive = await _tenantBillingGateway.ValidateTenantAsync(notification.TenantId, cancellationToken);
            if (!isTenantActive)
            {
                _logger.LogWarning("Tenant {TenantId} is not active or not authorized. Skipping notification delivery for event {EventId}",
                    notification.TenantId, notification.EventId);
                return new DeliveryResultDTO
                {
                    Success = false,
                    Error = "Tenant is not active or not authorized for delivery."
                };
            }
        }

        var attemptedChannels = 0;
        var settings = _settingsProvider.GetSettings(notification.TenantId);

        // A redelivered notification (consumer retry or reprocessed DLQ entry) must not resend on channels that
        // already succeeded: providers are not idempotent and an SMS would be delivered twice.
        var deliveredChannels = (await _deliveryRepository.GetByEventAsync(notification.EventId))
            .Where(a => string.Equals(a.TenantId, notification.TenantId, StringComparison.OrdinalIgnoreCase))
            .Where(a => a.StatusEnum is DeliveryStatusEnum.Sent or DeliveryStatusEnum.Retried)
            .GroupBy(a => a.Channel)
            .ToDictionary(g => g.Key, g => g.First().Provider);
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

            if (deliveredChannels.TryGetValue(channel, out var deliveredBy))
            {
                _logger.LogInformation("Event {EventId} was already delivered on channel {Channel}; skipping",
                    notification.EventId, channel);
                successfulProviders.Add(deliveredBy);
                continue;
            }

            var provider = _providers.FirstOrDefault(p => p.ChannelTypeEnum == channel);
            if (provider == null)
            {
                _logger.LogWarning("No provider registered for channel {Channel}.", channel);
                failedChannels.Add(channel.ToString());
                continue;
            }

            // MaxRetries is the total number of attempts per channel; the engine caps it at the platform limit.
            var maxAttempts = settings.MaxRetries.GetValueOrDefault(channel, 3);
            var attemptNumber = 0;
            var resilienceOptions = (provider as IResilientChannelProvider)?.ResilienceOptions ?? new ProviderResilienceDTO();
            attemptedChannels++;
            var attemptStopwatch = Stopwatch.StartNew();

            var attemptResult = await _retryPolicyEngine.ExecuteAsync(provider.ProviderName, async ct =>
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
            }, maxAttempts, resilienceOptions, cancellationToken);

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
                    attemptNumber,
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