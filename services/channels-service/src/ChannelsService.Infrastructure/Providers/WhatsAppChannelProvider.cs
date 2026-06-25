using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Infrastructure.Providers;

public sealed class WhatsAppChannelProvider : ChannelProviderBase
{
    public WhatsAppChannelProvider(ILogger<WhatsAppChannelProvider> logger) : base(logger) { }
    public override ChannelTypeEnum ChannelTypeEnum => ChannelTypeEnum.WhatsApp;
    public override async Task<DeliveryResult> SendAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sending WhatsApp notification for event {EventId} to tenant {TenantId}.", notification.EventId, notification.TenantId);
        await Task.Delay(50, cancellationToken);
        return new DeliveryResult { Success = true };
    }
}