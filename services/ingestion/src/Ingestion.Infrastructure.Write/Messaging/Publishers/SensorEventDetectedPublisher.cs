using Confluent.Kafka;
using Ingestion.Application.Events;
using Ingestion.Application.Interfaces.Publishers;
using Microsoft.Extensions.Logging;

namespace Ingestion.Infrastructure.Write.Messaging.Publishers;

public class SensorEventDetectedPublisher : IPublisher
{
    private readonly IProducer<Null, string> _producer;
    private readonly ILogger<SensorEventDetectedPublisher> _logger;

    public SensorEventDetectedPublisher(IProducer<Null, string> producer, ILogger<SensorEventDetectedPublisher> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    /// <summary>Throws when the broker does not acknowledge the message, so the outbox row stays pending.</summary>
    public async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        var message = new Message<Null, string> { Value = payload, Headers = new Headers() };
        using var activity = KafkaTracing.StartPublish(topic, message.Headers);
        var response = await _producer.ProduceAsync(topic, message, cancellationToken);

        _logger.LogInformation(
            "{EventType} message published successfully in Kafka with status {status}; partition {partition}; offset {offset}",
            nameof(SensorEventDetected),
            response.Status,
            response.Partition,
            response.Offset
        );
    }

    /// <summary>
    /// Nothing to release: the producer is a DI singleton disposed by the container, and every
    /// <see cref="PublishAsync"/> awaits the broker acknowledgement, so no message is left queued. Flushing here ran
    /// after the container had already disposed the producer (ObjectDisposedException on shutdown).
    /// </summary>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}