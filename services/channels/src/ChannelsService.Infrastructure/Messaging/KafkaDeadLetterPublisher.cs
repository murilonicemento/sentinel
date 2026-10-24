using System.Text.Json;
using System.Text.Json.Serialization;
using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Events;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Infrastructure.Messaging;

public sealed class KafkaDeadLetterPublisher : IDeadLetterPublisher, IAsyncDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly string _topic;
    private readonly IChannelMetrics _metrics;
    private readonly ILogger<KafkaDeadLetterPublisher> _logger;

    public KafkaDeadLetterPublisher(IConfiguration configuration, IChannelMetrics metrics, ILogger<KafkaDeadLetterPublisher> logger)
    {
        _metrics = metrics;
        _logger = logger;
        _topic = configuration["Kafka:DeadLetterTopic"] ?? "notification-events-dlq";

        var bootstrapServers = configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            _logger.LogWarning("Kafka bootstrap servers are not configured; dead-letter publishing will be disabled.");
        }

        var kafkaConfig = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        };

        _producer = new ProducerBuilder<Null, string>(kafkaConfig).Build();
    }

    public async Task PublishAsync(NotificationEvent notification, DeliveryResultDTO resultDto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_topic))
        {
            _logger.LogWarning("Dead-letter topic is not configured. Skipping DLQ publish.");
            return;
        }

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                notification,
                result = resultDto,
                publishedAt = DateTime.UtcNow
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            });

            var message = new Message<Null, string> { Value = payload };
            var delivery = await _producer.ProduceAsync(_topic, message, cancellationToken);
            _logger.LogInformation("Published notification {EventId} to DLQ topic {Topic} at offset {Offset}.", notification.EventId, _topic, delivery.Offset.Value);
            _metrics.RecordDeadLetterPublished(true);
        }
        catch (ProduceException<Null, string> exception)
        {
            _metrics.RecordDeadLetterPublished(false);
            _logger.LogError(exception, "Failed to publish notification {EventId} to DLQ topic {Topic}.", notification.EventId, _topic);
        }
        catch (Exception exception)
        {
            _metrics.RecordDeadLetterPublished(false);
            _logger.LogError(exception, "Unexpected error publishing notification {EventId} to DLQ.", notification.EventId);
        }
    }

    public ValueTask DisposeAsync()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        return ValueTask.CompletedTask;
    }
}
