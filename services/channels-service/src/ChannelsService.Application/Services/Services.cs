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

        _logger.LogInformation("Starting delivery for event {EventId} on tenant {TenantId}. Channels: {Channels}",
            notification.EventId,
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

            var maxRetries = settings.MaxRetries.TryGetValue(channel, out var retries) ? retries : 3;
            var attemptNumber = 0;

            var attemptResult = await _retryPolicyEngine.ExecuteAsync(async () =>
            {
                attemptNumber++;
                var deliveryResult = await provider.SendAsync(notification, cancellationToken);
                var status = deliveryResult.Success ? DeliveryStatus.Sent : DeliveryStatus.Failed;

                await _deliveryRepository.AddAsync(new DeliveryAttempt
                {
                    EventId = notification.EventId,
                    TenantId = notification.TenantId,
                    Channel = channel,
                    Status = status,
                    AttemptCount = attemptNumber,
                    ErrorMessage = deliveryResult.Error,
                    Timestamp = DateTime.UtcNow
                });

                return deliveryResult;
            }, maxRetries, cancellationToken);

            if (attemptResult.Success)
            {
                _logger.LogInformation("Delivery succeeded for event {EventId} on channel {Channel}.", notification.EventId, channel);
                return attemptResult;
            }

            _logger.LogWarning("Channel {Channel} failed for event {EventId}: {Error}", channel, notification.EventId, attemptResult.Error);
        }

        return new DeliveryResult { Success = false, Error = "All configured channels failed." };
    }
}

public sealed class RetryPolicyEngine : IRetryPolicyEngine
{
    private readonly ILogger<RetryPolicyEngine> _logger;

    public RetryPolicyEngine(ILogger<RetryPolicyEngine> logger)
    {
        _logger = logger;
    }

    public async Task<DeliveryResult> ExecuteAsync(Func<Task<DeliveryResult>> sendFunc, int maxAttempts, CancellationToken cancellationToken)
    {
        DeliveryResult lastResult = new() { Success = false, Error = "No send attempt executed." };

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new DeliveryResult { Success = false, Error = "Retry cancelled." };
            }

            try
            {
                lastResult = await sendFunc();
                if (lastResult.Success)
                {
                    return lastResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error executing retry policy on attempt {Attempt}.", attempt);
                lastResult = new DeliveryResult { Success = false, Error = ex.Message };
            }

            if (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));
                await Task.Delay(delay, cancellationToken);
            }
        }

        return lastResult;
    }
}

public sealed class FallbackExecutor : IFallbackExecutor
{
    public IReadOnlyList<ChannelType> GetFallbackOrder(NotificationEvent notification, TenantChannelSettings settings)
    {
        var requestedChannels = notification.Channels?.Distinct().ToList() ?? new List<ChannelType>();
        var enabledChannels = settings.EnabledChannels.Any() ? settings.EnabledChannels : Enum.GetValues<ChannelType>().Cast<ChannelType>().ToList();

        var candidateChannels = requestedChannels.Any()
            ? requestedChannels.Where(settings.EnabledChannels.Contains).ToList()
            : enabledChannels;

        if (settings.FallbackOrder.Any())
        {
            return settings.FallbackOrder
                .Where(candidateChannels.Contains)
                .Distinct()
                .ToList();
        }

        return candidateChannels.Distinct().ToList();
    }
}
