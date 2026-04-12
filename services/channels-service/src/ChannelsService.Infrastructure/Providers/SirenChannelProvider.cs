using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Models;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Infrastructure.Providers;

public sealed class SirenChannelProvider : ChannelProviderBase
{
    public SirenChannelProvider(ILogger<SirenChannelProvider> logger) : base(logger) { }
    public override ChannelType ChannelType => ChannelType.Siren;
    public override async Task<DeliveryResult> SendAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Triggering siren for event {EventId} to tenant {TenantId}.", notification.EventId, notification.TenantId);
        await Task.Delay(50, cancellationToken);
        return new DeliveryResult { Success = true };
    }
}