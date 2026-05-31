using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Models;
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

    public override ChannelType ChannelType => ChannelType.Sms;
    public override string ProviderName => "Twilio";

    public override async Task<DeliveryResult> SendAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        var recipientNumber = notification.Metadata?.GetValueOrDefault("recipientPhone") ?? notification.UserId;
        if (string.IsNullOrWhiteSpace(recipientNumber))
        {
            return new DeliveryResult { Success = false, Error = "Recipient phone number is missing." };
        }

        try
        {
            await MessageResource.CreateAsync(
                to: new PhoneNumber(recipientNumber),
                from: new PhoneNumber(_options.FromNumber),
                body: notification.Message.Body);

            _logger.LogInformation("Twilio sent SMS for event {EventId} to {Recipient}.", notification.EventId, recipientNumber);
            return new DeliveryResult { Success = true, ProviderName = ProviderName };
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Twilio send failed for event {EventId}.", notification.EventId);
            return new DeliveryResult
            {
                Success = false,
                Error = exception.Message,
                ProviderName = ProviderName
            };
        }
    }
}
