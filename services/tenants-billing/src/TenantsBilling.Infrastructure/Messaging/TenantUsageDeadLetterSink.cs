using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using TenantsBilling.Infrastructure.Messaging.Reliability;

namespace TenantsBilling.Infrastructure.Messaging;

/// <summary>
/// Publishes to <c>tenant-usage-events-dlq</c> in the format read by <c>TenantDeadLetterConsumerHostedService</c>
/// (<c>sourceTopic</c>, <c>reason</c>, <c>attempts</c>, <c>partition</c>, <c>offset</c>, <c>originalPayload</c>), plus
/// the stable <c>errorCode</c>, <c>failedAt</c> and <c>consumerGroup</c> fields.
/// </summary>
public sealed class TenantUsageDeadLetterSink : IDeadLetterSink, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly int _attempts;
    private readonly IProducer<string?, string> _producer;
    private readonly string _topic;

    public TenantUsageDeadLetterSink(string bootstrapServers, string topic, int attempts)
    {
        _topic = topic;
        _attempts = attempts;
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
        var payload = Serialize(envelope, _attempts);

        var headers = new Headers();
        if (originalHeaders is not null)
            foreach (var header in originalHeaders)
                headers.Add(header.Key, header.GetValueBytes());
        headers.Add("x-error-code", Encoding.UTF8.GetBytes(envelope.ErrorCode));

        // The original headers are copied above; a new span replaces their traceparent as a child of the failed
        // processing, so the dead letter shows up in the same trace.
        using var activity = KafkaTracing.StartPublish(_topic, headers);
        await _producer.ProduceAsync(_topic,
            new Message<string?, string> { Key = envelope.Key, Value = payload, Headers = headers }, cancellationToken);
    }

    /// <summary>Serializes the envelope in the format parsed by <c>TenantDeadLetterConsumerHostedService</c>.</summary>
    public static string Serialize(DeadLetterEnvelope envelope, int attempts) => JsonSerializer.Serialize(new
    {
        sourceTopic = envelope.OriginalTopic,
        reason = envelope.ErrorReason,
        errorCode = envelope.ErrorCode,
        attempts,
        partition = envelope.Partition,
        offset = envelope.Offset,
        originalPayload = envelope.Payload,
        failedAt = envelope.FailedAt,
        consumerGroup = envelope.ConsumerGroup
    }, JsonOptions);

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
