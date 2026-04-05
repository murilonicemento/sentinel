using System.Text.Json;
using AlertOrchestrator.Application.Interfaces.Messaging;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Infrastructure.Messaging.Publishers;

public sealed class KafkaEventPublisher : IEventPublisher
{
    private readonly ILogger<KafkaEventPublisher> _logger;
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public KafkaEventPublisher(string bootstrapServers, string topic, ILogger<KafkaEventPublisher> logger)
    {
        var config = new ProducerConfig { BootstrapServers = bootstrapServers };
        _producer = new ProducerBuilder<string, string>(config).Build();
        _topic = topic;
        _logger = logger;
        _logger.LogInformation("KafkaEventPublisher initialized. BootstrapServers: {BootstrapServers}, Topic: {Topic}",
            bootstrapServers, topic);
    }

    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class
    {
        var eventType = typeof(T).Name;
        _logger.LogInformation("Publishing event of type: {EventType} to topic: {Topic}", eventType, _topic);

        var message = JsonSerializer.Serialize(@event);
        var kafkaMessage = new Message<string, string>
        {
            Key = Guid.NewGuid().ToString(),
            Value = message
        };

        try
        {
            var result = await _producer.ProduceAsync(_topic, kafkaMessage, cancellationToken);
            _logger.LogInformation(
                "Event published successfully. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
                result.Topic, result.Partition, result.Offset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event to topic: {Topic}", _topic);
            throw;
        }
    }
}