using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChannelsService.Infrastructure.Providers;

public sealed class PushChannelProvider : ChannelProviderBase
{
    private readonly PushProviderOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public PushChannelProvider(
        ILogger<PushChannelProvider> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<ChannelProviderSettings<PushProviderOptions>> options)
        : base(logger, options.Value.Resilience)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value.Options;
    }

    public override ChannelTypeEnum ChannelTypeEnum => ChannelTypeEnum.Push;
    public override string ProviderName => "PushService";

    public override async Task<DeliveryResult> SendAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            return new DeliveryResult
            {
                Success = false,
                ProviderName = ProviderName,
                Error = "Push provider endpoint is not configured."
            };
        }

        var target = notification.Metadata?.GetValueOrDefault("pushTarget") ?? _options.DefaultTarget;
        if (string.IsNullOrWhiteSpace(target))
        {
            return new DeliveryResult
            {
                Success = false,
                ProviderName = ProviderName,
                Error = "Push target is missing."
            };
        }

        var payload = new
        {
            notification.EventId,
            notification.TenantId,
            notification.EventType,
            notification.CorrelationId,
            notification.Message.Title,
            notification.Message.Body,
            Target = target,
            Metadata = notification.Metadata
        };

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(PushChannelProvider));
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            }

            var response = await client.PostAsJsonAsync(_options.Endpoint, payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Push sent for event {EventId} to target {Target}.", notification.EventId, target);
                return new DeliveryResult { Success = true, ProviderName = ProviderName };
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Push provider returned {StatusCode} for event {EventId}: {Body}.", response.StatusCode, notification.EventId, responseBody);
            return new DeliveryResult
            {
                Success = false,
                ProviderName = ProviderName,
                Error = $"Push provider returned {(int)response.StatusCode}: {responseBody}"
            };
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Push send failed for event {EventId}.", notification.EventId);
            return new DeliveryResult
            {
                Success = false,
                ProviderName = ProviderName,
                Error = exception.Message
            };
        }
    }
}
