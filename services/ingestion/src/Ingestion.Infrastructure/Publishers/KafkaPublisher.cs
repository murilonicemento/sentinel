using Confluent.Kafka;
using Ingestion.Application.Interfaces.Publishers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ingestion.Infrastructure.Publishers;

public class KafkaPublisher : IPublisher
{
    private readonly IProducer<Null, string> _producer;
    private readonly ILogger<KafkaPublisher> _logger;

    public KafkaPublisher(IProducer<Null, string> producer, ILogger<KafkaPublisher> logger)
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
                "Message published with success in Kafka with status {status}; partition {partition}; offset {offset}",
                response.Status,
                response.Partition,
                response.Offset
            );
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to publish message in Kafka.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_producer is IAsyncDisposable producerAsyncDisposable)
            await producerAsyncDisposable.DisposeAsync();
        else
            _producer.Flush(TimeSpan.FromSeconds(5));
    }
}