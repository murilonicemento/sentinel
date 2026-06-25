using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChannelsService.Infrastructure.Providers;

public sealed class SirenChannelProvider : ChannelProviderBase
{
    private readonly SirenProviderOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public SirenChannelProvider(
        ILogger<SirenChannelProvider> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<ChannelProviderSettings<SirenProviderOptions>> options)
        : base(logger, options.Value.Resilience)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value.Options;
    }

    public override ChannelTypeEnum ChannelTypeEnum => ChannelTypeEnum.Siren;
    public override string ProviderName => "SirenService";

    public override async Task<DeliveryResult> SendAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            return new DeliveryResult
            {
                Success = false,
                ProviderName = ProviderName,
                Error = "Siren provider endpoint is not configured."
            };
        }

        var deviceId = notification.Metadata?.GetValueOrDefault("sirenDeviceId") ?? _options.DefaultDeviceId;
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return new DeliveryResult
            {
                Success = false,
                ProviderName = ProviderName,
                Error = "Siren device id is missing."
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
            DeviceId = deviceId,
            Metadata = notification.Metadata
        };

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(SirenChannelProvider));
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            }

            var response = await client.PostAsJsonAsync(_options.Endpoint, payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Siren triggered for event {EventId} on device {DeviceId}.", notification.EventId, deviceId);
                return new DeliveryResult { Success = true, ProviderName = ProviderName };
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Siren provider returned {StatusCode} for event {EventId}: {Body}.", response.StatusCode, notification.EventId, responseBody);
            return new DeliveryResult
            {
                Success = false,
                ProviderName = ProviderName,
                Error = $"Siren provider returned {(int)response.StatusCode}: {responseBody}"
            };
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Siren send failed for event {EventId}.", notification.EventId);
            return new DeliveryResult
            {
                Success = false,
                ProviderName = ProviderName,
                Error = exception.Message
            };
        }
    }
}
