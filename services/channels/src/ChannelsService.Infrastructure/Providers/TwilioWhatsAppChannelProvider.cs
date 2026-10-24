using System.Text.Json;
using ChannelsService.Application.DTOs;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Events;
using ChannelsService.Infrastructure.Options;
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

    public override async Task<DeliveryResultDTO> SendAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        var recipientNumber = notification.Metadata?.GetValueOrDefault("recipientPhone") ?? notification.UserId;
        if (string.IsNullOrWhiteSpace(recipientNumber))
        {
            return new DeliveryResultDTO { Success = false, Error = "Recipient phone number is missing." };
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
            return new DeliveryResultDTO { Success = true, ProviderName = ProviderName };
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Twilio WhatsApp send failed for event {EventId}.", notification.EventId);
            return new DeliveryResultDTO
            {
                Success = false,
                Error = exception.Message,
                ProviderName = ProviderName
            };
        }
    }
}
