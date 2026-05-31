using System.Text.Json;
using System.Text.Json.Serialization;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Models;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Infrastructure.Messaging;

public sealed class KafkaNotificationPublisher : INotificationPublisher, IAsyncDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly string _topic;
    private readonly ILogger<KafkaNotificationPublisher> _logger;

    public KafkaNotificationPublisher(IConfiguration configuration, ILogger<KafkaNotificationPublisher> logger)
    {
        _logger = logger;
        _topic = configuration["Kafka:Topic"] ?? "notification-events";

        var bootstrapServers = configuration["Kafka:BootstrapServers"];
        var kafkaConfig = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        };

        _producer = new ProducerBuilder<Null, string>(kafkaConfig).Build();
    }

    public async Task PublishAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(notification, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        });

        var message = new Message<Null, string> { Value = payload };
        await _producer.ProduceAsync(_topic, message, cancellationToken);
        _logger.LogInformation("Republished notification {EventId} to topic {Topic}.", notification.EventId, _topic);
    }

    public ValueTask DisposeAsync()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        return ValueTask.CompletedTask;
    }
}
