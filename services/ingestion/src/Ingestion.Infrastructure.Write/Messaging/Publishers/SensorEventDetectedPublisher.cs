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

    public async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new Message<Null, string> { Value = payload };
            var response = await _producer.ProduceAsync(topic, message, cancellationToken);

            _logger.LogInformation(
                "{EventType} message published successfully in Kafka with status {status}; partition {partition}; offset {offset}",
                nameof(SensorEventDetected),
                response.Status,
                response.Partition,
                response.Offset
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish {EventType} message in Kafka.", nameof(SensorEventDetected));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_producer is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            _producer.Flush(TimeSpan.FromSeconds(5));
    }
}