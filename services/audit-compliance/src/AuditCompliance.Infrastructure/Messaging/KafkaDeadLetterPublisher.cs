using System.Text.Json;
using AuditCompliance.Infrastructure.Options;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace AuditCompliance.Infrastructure.Messaging;

public sealed class KafkaDeadLetterPublisher : IDisposable
{
    private readonly IProducer<string?, string> _producer;
    private readonly string _topic;

    public KafkaDeadLetterPublisher(IOptions<KafkaOptions> options)
    {
        _topic = options.Value.DeadLetterTopic;
        _producer = new ProducerBuilder<string?, string>(new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = 30_000
        }).Build();
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }

    /// <summary>
    /// Publica e aguarda a confirmação do broker; a mensagem original só pode ser confirmada depois disso.
    /// </summary>
    public async Task PublishAsync(DeadLetterEnvelope envelope, Headers? originalHeaders,
        CancellationToken cancellationToken)
    {
        var headers = new Headers();
        if (originalHeaders is not null)
            foreach (var header in originalHeaders)
                headers.Add(header.Key, header.GetValueBytes());

        headers.Add("x-error-code", System.Text.Encoding.UTF8.GetBytes(envelope.ErrorCode));

        await _producer.ProduceAsync(_topic, new Message<string?, string>
        {
            Key = envelope.Key,
            Value = JsonSerializer.Serialize(envelope),
            Headers = headers
        }, cancellationToken);
    }
}
