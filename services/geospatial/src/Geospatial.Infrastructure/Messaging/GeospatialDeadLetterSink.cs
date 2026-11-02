using System.Text.Json;
using Confluent.Kafka;
using Geospatial.Infrastructure.Messaging.Reliability;
using LegacyDeadLetterEnvelope = Geospatial.Infrastructure.Messaging.Events.DeadLetterEnvelope;

namespace Geospatial.Infrastructure.Messaging;

/// <summary>
/// Publishes to <c>geospatial-dead-letter</c> in its existing format (<c>OriginalTopic</c>, <c>OriginalPartition</c>,
/// <c>OriginalOffset</c>, <c>Payload</c>, <c>FailedAt</c>) plus the additive <c>ErrorCode</c> and <c>ErrorReason</c>.
/// </summary>
public sealed class GeospatialDeadLetterSink : IDeadLetterSink
{
    private readonly IProducer<Null, string> _producer;
    private readonly string _topic;

    public GeospatialDeadLetterSink(IProducer<Null, string> producer, string topic)
    {
        _producer = producer;
        _topic = topic;
    }

    public async Task PublishAsync(DeadLetterEnvelope envelope, Headers? originalHeaders,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new LegacyDeadLetterEnvelope
        {
            OriginalTopic = envelope.OriginalTopic,
            OriginalPartition = envelope.Partition,
            OriginalOffset = envelope.Offset,
            Payload = envelope.Payload,
            FailedAt = envelope.FailedAt.UtcDateTime,
            ErrorCode = envelope.ErrorCode,
            ErrorReason = envelope.ErrorReason
        });

        var message = new Message<Null, string> { Value = payload, Headers = new Headers() };
        using var activity = KafkaTracing.StartPublish(_topic, message.Headers);
        await _producer.ProduceAsync(_topic, message, cancellationToken);
    }
}
