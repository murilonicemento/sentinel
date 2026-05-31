using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Interfaces;
using ChannelsService.Domain.Models;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Infrastructure.Providers;

public abstract class ChannelProviderBase : IChannelProvider, IResilientChannelProvider
{
    protected readonly ILogger _logger;

    protected ChannelProviderBase(ILogger logger, ProviderResilienceOptions? resilienceOptions = null)
    {
        _logger = logger;
        ResilienceOptions = resilienceOptions ?? new();
    }

    public ProviderResilienceOptions ResilienceOptions { get; }

    public abstract ChannelType ChannelType { get; }
    public virtual string ProviderName => ChannelType.ToString();
    public abstract Task<DeliveryResult> SendAsync(NotificationEvent notification, CancellationToken cancellationToken);
}