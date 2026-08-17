using Confluent.Kafka;
using TenantsBilling.Domain.Events;

namespace TenantsBilling.Infrastructure.Messaging;

public sealed class KafkaTenantEventPublisher : ITenantEventPublisher
{
    private readonly IProducer<Null, string> _producer;
    private readonly string _topic;

    public KafkaTenantEventPublisher(IProducer<Null, string> producer, string topic)
    {
        _producer = producer;
        _topic = topic;
    }

    public async Task PublishAsync(string eventType, object payload, CancellationToken cancellationToken = default)
    {
        var message = new
        {
            eventType,
            occurredAt = DateTime.UtcNow,
            payload
        };

        var json = System.Text.Json.JsonSerializer.Serialize(message);
        await _producer.ProduceAsync(_topic, new Message<Null, string> { Value = json }, cancellationToken);
    }
}
