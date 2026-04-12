using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Interfaces;
using ChannelsService.Domain.Models;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Infrastructure.Providers;

public abstract class ChannelProviderBase : IChannelProvider
{
    protected readonly ILogger _logger;

    protected ChannelProviderBase(ILogger logger)
    {
        _logger = logger;
    }

    public abstract ChannelType ChannelType { get; }
    public abstract Task<DeliveryResult> SendAsync(NotificationEvent notification, CancellationToken cancellationToken);
}