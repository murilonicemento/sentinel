using System.Text.Json;
using Confluent.Kafka;
using TenantsBilling.Application.Auditing;

namespace TenantsBilling.Infrastructure.Messaging;

/// <summary>
/// Publishes <c>audit-events</c> (<c>Kafka:AuditTopic</c>) in the audit-compliance v1 envelope, keyed by tenant as
/// audit-compliance recommends.
/// </summary>
public sealed class KafkaAuditEventPublisher : IAuditEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public KafkaAuditEventPublisher(string bootstrapServers, string topic)
    {
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        }).Build();
        _topic = topic;
    }

    public async Task PublishAsync(AuditEventEnvelope auditEvent, CancellationToken cancellationToken = default)
    {
        var message = new Message<string, string>
        {
            Key = auditEvent.TenantId,
            Value = JsonSerializer.Serialize(auditEvent),
            Headers = new Headers()
        };
        using var activity = KafkaTracing.StartPublish(_topic, message.Headers);
        await _producer.ProduceAsync(_topic, message, cancellationToken);
    }

    public void Dispose() => _producer.Dispose();
}
