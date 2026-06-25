using System.Text.Json;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace ChannelsService.Infrastructure.Providers;

public sealed class TwilioWhatsAppChannelProvider : ChannelProviderBase
{
    private readonly TwilioProviderOptions _options;

    public TwilioWhatsAppChannelProvider(
        ILogger<TwilioWhatsAppChannelProvider> logger,
        IOptions<ChannelProviderSettings<TwilioProviderOptions>> options)
        : base(logger, options.Value.Resilience)
    {
        _options = options.Value.Options;
        TwilioClient.Init(_options.AccountSid, _options.AuthToken);
    }

    public override ChannelTypeEnum ChannelTypeEnum => ChannelTypeEnum.WhatsApp;
    public override string ProviderName => "TwilioWhatsApp";

    public override async Task<DeliveryResult> SendAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        var recipientNumber = notification.Metadata?.GetValueOrDefault("recipientPhone") ?? notification.UserId;
        if (string.IsNullOrWhiteSpace(recipientNumber))
        {
            return new DeliveryResult { Success = false, Error = "Recipient phone number is missing." };
        }

        try
        {
            var from = new PhoneNumber($"whatsapp:{_options.FromNumber}");
            var to = new PhoneNumber($"whatsapp:{recipientNumber}");
            await MessageResource.CreateAsync(
                to: to,
                from: from,
                body: notification.Message.Body);

            _logger.LogInformation("Twilio WhatsApp sent message for event {EventId} to {Recipient}.", notification.EventId, recipientNumber);
            return new DeliveryResult { Success = true, ProviderName = ProviderName };
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Twilio WhatsApp send failed for event {EventId}.", notification.EventId);
            return new DeliveryResult
            {
                Success = false,
                Error = exception.Message,
                ProviderName = ProviderName
            };
        }
    }
}
