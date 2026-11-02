using ChannelsService.Application.DTOs;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Events;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Infrastructure.Providers;

public sealed class SmsChannelProvider : ChannelProviderBase
{
    public SmsChannelProvider(ILogger<SmsChannelProvider> logger) : base(logger) { }
    public override ChannelTypeEnum ChannelTypeEnum => ChannelTypeEnum.Sms;
    public override async Task<DeliveryResultDTO> SendAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sending SMS for event {EventId} to tenant {TenantId}.", notification.EventId, notification.TenantId);
        await Task.Delay(50, cancellationToken);
        return new DeliveryResultDTO { Success = true };
    }
}