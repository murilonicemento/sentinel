using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Models;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Infrastructure.Providers;

public sealed class EmailChannelProvider : ChannelProviderBase
{
    public EmailChannelProvider(ILogger<EmailChannelProvider> logger) : base(logger) { }
    public override ChannelType ChannelType => ChannelType.Email;
    public override async Task<DeliveryResult> SendAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sending Email for event {EventId} to tenant {TenantId}.", notification.EventId, notification.TenantId);
        await Task.Delay(50, cancellationToken);
        return new DeliveryResult { Success = true };
    }
}