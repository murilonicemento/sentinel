using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Interfaces;
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

    public abstract ChannelTypeEnum ChannelTypeEnum { get; }
    public virtual string ProviderName => ChannelTypeEnum.ToString();
    public abstract Task<DeliveryResult> SendAsync(NotificationEvent notification, CancellationToken cancellationToken);
}