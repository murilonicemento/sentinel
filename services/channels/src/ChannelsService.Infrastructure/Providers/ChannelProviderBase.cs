using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Events;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Infrastructure.Providers;

public abstract class ChannelProviderBase : IChannelProvider, IResilientChannelProvider
{
    protected readonly ILogger _logger;

    protected ChannelProviderBase(ILogger logger, ProviderResilienceDTO? resilienceOptions = null)
    {
        _logger = logger;
        ResilienceDto = resilienceOptions ?? new();
    }

    public ProviderResilienceDTO ResilienceDto { get; }

    public abstract ChannelTypeEnum ChannelTypeEnum { get; }
    public virtual string ProviderName => ChannelTypeEnum.ToString();

    public abstract Task<DeliveryResultDTO> SendAsync(NotificationEvent notification,
        CancellationToken cancellationToken);

    public ProviderResilienceDTO ResilienceOptions { get; }
}