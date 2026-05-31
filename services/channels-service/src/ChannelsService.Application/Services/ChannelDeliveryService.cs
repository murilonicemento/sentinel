using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Interfaces;
using ChannelsService.Domain.Models;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Application.Services;

public sealed class ChannelDeliveryService : IChannelDeliveryService
{
    private readonly IEnumerable<IChannelProvider> _providers;
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ITenantChannelSettingsProvider _settingsProvider;
    private readonly IRetryPolicyEngine _retryPolicyEngine;
    private readonly IFallbackExecutor _fallbackExecutor;
    private readonly ILogger<ChannelDeliveryService> _logger;

    public ChannelDeliveryService(
        IEnumerable<IChannelProvider> providers,
        IDeliveryRepository deliveryRepository,
        ITenantChannelSettingsProvider settingsProvider,
        IRetryPolicyEngine retryPolicyEngine,
        IFallbackExecutor fallbackExecutor,
        ILogger<ChannelDeliveryService> logger)
    {
        _providers = providers;
        _deliveryRepository = deliveryRepository;
        _settingsProvider = settingsProvider;
        _retryPolicyEngine = retryPolicyEngine;
        _fallbackExecutor = fallbackExecutor;
        _logger = logger;
    }

    public async Task<DeliveryResult> DeliverAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        var settings = _settingsProvider.GetSettings(notification.TenantId);
        var orderedChannels = _fallbackExecutor.GetFallbackOrder(notification, settings);

        _logger.LogInformation(
            "Starting delivery for event {EventId} type {EventType} correlation {CorrelationId} on tenant {TenantId}. Channels: {Channels}",
            notification.EventId,
            notification.EventType,
            notification.CorrelationId,
            notification.TenantId,
            string.Join(',', orderedChannels));

        foreach (var channel in orderedChannels)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new DeliveryResult { Success = false, Error = "Delivery cancelled." };
            }

            var provider = _providers.FirstOrDefault(p => p.ChannelType == channel);
            if (provider == null)
            {
                _logger.LogWarning("No provider registered for channel {Channel}.", channel);
                continue;
            }

            var maxRetries = settings.MaxRetries.GetValueOrDefault(channel, 3);
            var attemptNumber = 0;
            var resilienceOptions = (provider as IResilientChannelProvider)?.ResilienceOptions ?? new ProviderResilienceOptions();

            var attemptResult = await _retryPolicyEngine.ExecuteAsync(async ct =>
            {
                attemptNumber++;
                var deliveryResult = await provider.SendAsync(notification, ct);
                deliveryResult.ProviderName = provider.ProviderName;

                var status = deliveryResult.Success
                    ? attemptNumber > 1 ? DeliveryStatus.Retried : DeliveryStatus.Sent
                    : DeliveryStatus.Failed;

                await _deliveryRepository.AddAsync(new DeliveryAttempt
                {
                    AttemptId = Guid.NewGuid().ToString("N"),
                    EventId = notification.EventId,
                    TenantId = notification.TenantId,
                    Channel = channel,
                    Provider = provider.ProviderName,
                    Status = status,
                    AttemptCount = attemptNumber,
                    ErrorMessage = deliveryResult.Error,
                    Timestamp = DateTime.UtcNow
                });

                return deliveryResult;
            }, maxRetries, resilienceOptions, cancellationToken);

            if (attemptResult.Success)
            {
                _logger.LogInformation(
                    "Delivery succeeded for event {EventId} on channel {Channel} using provider {Provider}.",
                    notification.EventId,
                    channel,
                    attemptResult.ProviderName);
                return attemptResult;
            }

            _logger.LogWarning(
                "Channel {Channel} failed for event {EventId} after {Attempts} attempts: {Error}",
                channel,
                notification.EventId,
                maxRetries,
                attemptResult.Error);
        }

        return new DeliveryResult { Success = false, Error = "All configured channels failed." };
    }
}