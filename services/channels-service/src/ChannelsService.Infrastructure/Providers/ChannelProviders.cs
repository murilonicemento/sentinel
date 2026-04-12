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

public sealed class SmsChannelProvider : ChannelProviderBase
{
    public SmsChannelProvider(ILogger<SmsChannelProvider> logger) : base(logger) { }
    public override ChannelType ChannelType => ChannelType.Sms;
    public override async Task<DeliveryResult> SendAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sending SMS for event {EventId} to tenant {TenantId}.", notification.EventId, notification.TenantId);
        await Task.Delay(50, cancellationToken);
        return new DeliveryResult { Success = true };
    }
}

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

public sealed class PushChannelProvider : ChannelProviderBase
{
    public PushChannelProvider(ILogger<PushChannelProvider> logger) : base(logger) { }
    public override ChannelType ChannelType => ChannelType.Push;
    public override async Task<DeliveryResult> SendAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sending Push notification for event {EventId} to tenant {TenantId}.", notification.EventId, notification.TenantId);
        await Task.Delay(50, cancellationToken);
        return new DeliveryResult { Success = true };
    }
}

public sealed class WhatsAppChannelProvider : ChannelProviderBase
{
    public WhatsAppChannelProvider(ILogger<WhatsAppChannelProvider> logger) : base(logger) { }
    public override ChannelType ChannelType => ChannelType.WhatsApp;
    public override async Task<DeliveryResult> SendAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sending WhatsApp notification for event {EventId} to tenant {TenantId}.", notification.EventId, notification.TenantId);
        await Task.Delay(50, cancellationToken);
        return new DeliveryResult { Success = true };
    }
}

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
