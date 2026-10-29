using System.Text;
using System.Text.Json;
using Confluent.Kafka;

namespace TenantsBilling.Infrastructure.Messaging.Reliability;

public interface IDeadLetterSink
{
    /// <summary>Publishes the envelope and waits for the broker acknowledgement.</summary>
    Task PublishAsync(DeadLetterEnvelope envelope, Headers? originalHeaders, CancellationToken cancellationToken);
}

public sealed class KafkaDeadLetterSink : IDeadLetterSink, IDisposable
{
    private readonly IProducer<string?, string> _producer;
    private readonly string _topic;

    public KafkaDeadLetterSink(string bootstrapServers, string topic)
    {
        _topic = topic;
        _producer = new ProducerBuilder<string?, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = 30_000
        }).Build();
    }

    public async Task PublishAsync(DeadLetterEnvelope envelope, Headers? originalHeaders,
        CancellationToken cancellationToken)
    {
        var headers = new Headers();
        if (originalHeaders is not null)
            foreach (var header in originalHeaders)
                headers.Add(header.Key, header.GetValueBytes());
        headers.Add("x-error-code", Encoding.UTF8.GetBytes(envelope.ErrorCode));

        // The original headers are copied above; a new span replaces their traceparent as a child of the failed
        // processing, so the dead letter shows up in the same trace.
        using var activity = KafkaTracing.StartPublish(_topic, headers);
        await _producer.ProduceAsync(_topic, new Message<string?, string>
        {
            Key = envelope.Key,
            Value = JsonSerializer.Serialize(envelope),
            Headers = headers
        }, cancellationToken);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
