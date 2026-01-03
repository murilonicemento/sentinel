using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using RiskCatalog.Application.Interfaces;

namespace RiskCatalog.Infrastructure.Messaging;

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
                "Message published with success in Kafka. Topic: {topic}, Status: {status}, Partition: {partition}, Offset: {offset}",
                topic,
                response.Status,
                response.Partition,
                response.Offset);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to publish message in Kafka. Topic: {topic}", topic);
            throw;
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

