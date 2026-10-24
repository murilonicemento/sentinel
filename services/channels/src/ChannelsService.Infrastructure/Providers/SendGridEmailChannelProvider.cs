using ChannelsService.Application.DTOs;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Events;
using ChannelsService.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace ChannelsService.Infrastructure.Providers;

public sealed class SendGridEmailChannelProvider : ChannelProviderBase
{
    private readonly SendGridClient _client;
    private readonly SendGridProviderOptions _options;

    public SendGridEmailChannelProvider(
        ILogger<SendGridEmailChannelProvider> logger,
        IOptions<ChannelProviderSettings<SendGridProviderOptions>> options)
        : base(logger, options.Value.Resilience)
    {
        _options = options.Value.Options;
        _client = new SendGridClient(_options.ApiKey);
    }

    public override ChannelTypeEnum ChannelTypeEnum => ChannelTypeEnum.Email;
    public override string ProviderName => "SendGrid";

    public override async Task<DeliveryResultDTO> SendAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        var recipientEmail = notification.Metadata?.GetValueOrDefault("recipientEmail") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            return new DeliveryResultDTO { Success = false, Error = "Recipient email is missing." };
        }

        var from = new EmailAddress(_options.FromEmail, _options.FromName);
        var to = new EmailAddress(recipientEmail);
        var message = MailHelper.CreateSingleEmail(from, to, notification.Message.Title, notification.Message.Body, notification.Message.Body);

        var response = await _client.SendEmailAsync(message, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("SendGrid sent email for event {EventId} to {Recipient}.", notification.EventId, recipientEmail);
            return new DeliveryResultDTO { Success = true, ProviderName = ProviderName };
        }

        var responseBody = await response.Body.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning("SendGrid failed for event {EventId} with status {StatusCode}: {Body}.", notification.EventId, response.StatusCode, responseBody);
        return new DeliveryResultDTO
        {
            Success = false,
            Error = $"SendGrid returned {(int)response.StatusCode}: {responseBody}",
            ProviderName = ProviderName
        };
    }
}
