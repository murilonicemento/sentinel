using System.Text.Json;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace ChannelsService.Infrastructure.Providers;

public sealed class MqttChannelProvider : ChannelProviderBase
{
    private readonly MqttProviderOptions _options;

    public MqttChannelProvider(
        ILogger<MqttChannelProvider> logger,
        IOptions<ChannelProviderSettings<MqttProviderOptions>> options)
        : base(logger, options.Value.Resilience)
    {
        _options = options.Value.Options;
    }

    public override ChannelTypeEnum ChannelTypeEnum => ChannelTypeEnum.Mqtt;
    public override string ProviderName => "MQTT";

    public override async Task<DeliveryResult> SendAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Broker))
        {
            return new DeliveryResult { Success = false, Error = "MQTT broker is not configured." };
        }

        var topic = notification.Metadata?.GetValueOrDefault("mqttTopic") ?? _options.Topic;
        if (string.IsNullOrWhiteSpace(topic))
        {
            return new DeliveryResult { Success = false, Error = "MQTT topic is not configured." };
        }

        var payload = JsonSerializer.Serialize(new
        {
            notification.EventId,
            notification.TenantId,
            notification.EventType,
            notification.CorrelationId,
            notification.Message,
            metadata = notification.Metadata
        });

        try
        {
            var clientOptions = new MqttClientOptionsBuilder()
                .WithClientId(string.IsNullOrWhiteSpace(_options.ClientId) ? Guid.NewGuid().ToString() : _options.ClientId)
                .WithTcpServer(_options.Broker, _options.Port)
                .Build();

            var mqttFactory = new MqttClientFactory();
            using var mqttClient = mqttFactory.CreateMqttClient();
            await mqttClient.ConnectAsync(clientOptions, cancellationToken);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            var result = await mqttClient.PublishAsync(message, cancellationToken);
            await mqttClient.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), cancellationToken);

            if (result.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                _logger.LogInformation("MQTT published notification {EventId} to topic {Topic}.", notification.EventId, topic);
                return new DeliveryResult { Success = true, ProviderName = ProviderName };
            }

            return new DeliveryResult { Success = false, Error = $"MQTT publish failed with reason {result.ReasonCode}.", ProviderName = ProviderName };
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "MQTT send failed for event {EventId}.", notification.EventId);
            return new DeliveryResult { Success = false, Error = exception.Message, ProviderName = ProviderName };
        }
    }
}
