using System.Text.Json;
using AlertOrchestrator.Application.Events;
using AlertOrchestrator.Application.Interfaces.Messaging;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Infrastructure.Messaging.Publishers;

/// <summary>Publishes <c>tenant-usage-events</c> keyed by tenant.</summary>
public sealed class KafkaTenantUsagePublisher : ITenantUsagePublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;
    private readonly ILogger<KafkaTenantUsagePublisher> _logger;

    public KafkaTenantUsagePublisher(string bootstrapServers, string topic, ILogger<KafkaTenantUsagePublisher> logger)
    {
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        }).Build();
        _topic = topic;
        _logger = logger;
    }

    public async Task PublishAsync(TenantUsageEvent usage, CancellationToken cancellationToken = default)
    {
        var message = new Message<string, string>
        {
            Key = usage.TenantId.ToString(),
            Value = JsonSerializer.Serialize(usage),
            Headers = new Headers()
        };

        using var activity = KafkaTracing.StartPublish(_topic, message.Headers);
        var result = await _producer.ProduceAsync(_topic, message, cancellationToken);
        _logger.LogInformation("Tenant usage {EventId} published to {Topic}@{Offset}", usage.EventId, result.Topic,
            result.Offset.Value);
    }

    public void Dispose() => _producer.Dispose();
}
