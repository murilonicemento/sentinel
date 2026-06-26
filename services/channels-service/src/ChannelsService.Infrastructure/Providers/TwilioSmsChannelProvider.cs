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

public sealed class TwilioSmsChannelProvider : ChannelProviderBase
{
    private readonly TwilioProviderOptions _options;

    public TwilioSmsChannelProvider(
        ILogger<TwilioSmsChannelProvider> logger,
        IOptions<ChannelProviderSettings<TwilioProviderOptions>> options)
        : base(logger, options.Value.Resilience)
    {
        _options = options.Value.Options;
        TwilioClient.Init(_options.AccountSid, _options.AuthToken);
    }

    public override ChannelTypeEnum ChannelTypeEnum => ChannelTypeEnum.Sms;
    public override string ProviderName => "Twilio";

    public override async Task<DeliveryResultDTO> SendAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        var recipientNumber = notification.Metadata?.GetValueOrDefault("recipientPhone") ?? notification.UserId;
        if (string.IsNullOrWhiteSpace(recipientNumber))
        {
            return new DeliveryResultDTO { Success = false, Error = "Recipient phone number is missing." };
        }

        try
        {
            await MessageResource.CreateAsync(
                to: new PhoneNumber(recipientNumber),
                from: new PhoneNumber(_options.FromNumber),
                body: notification.Message.Body);

            _logger.LogInformation("Twilio sent SMS for event {EventId} to {Recipient}.", notification.EventId, recipientNumber);
            return new DeliveryResultDTO { Success = true, ProviderName = ProviderName };
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Twilio send failed for event {EventId}.", notification.EventId);
            return new DeliveryResultDTO
            {
                Success = false,
                Error = exception.Message,
                ProviderName = ProviderName
            };
        }
    }
}
