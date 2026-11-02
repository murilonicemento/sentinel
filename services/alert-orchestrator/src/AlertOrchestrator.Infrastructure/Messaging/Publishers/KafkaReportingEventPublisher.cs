using System.Text.Json;
using AlertOrchestrator.Application.Events;
using AlertOrchestrator.Application.Interfaces.Messaging;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Infrastructure.Messaging.Publishers;

/// <summary>Publishes <c>reporting-events</c> keyed by tenant.</summary>
public sealed class KafkaReportingEventPublisher : IReportingEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;
    private readonly ILogger<KafkaReportingEventPublisher> _logger;

    public KafkaReportingEventPublisher(string bootstrapServers, string topic,
        ILogger<KafkaReportingEventPublisher> logger)
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

    public async Task PublishAsync(ReportingEventEnvelope reportingEvent, CancellationToken cancellationToken = default)
    {
        var message = new Message<string, string>
        {
            Key = reportingEvent.TenantId,
            Value = JsonSerializer.Serialize(reportingEvent),
            Headers = new Headers()
        };

        using var activity = KafkaTracing.StartPublish(_topic, message.Headers);
        var result = await _producer.ProduceAsync(_topic, message, cancellationToken);
        _logger.LogInformation("Reporting event {EventId} published to {Topic}@{Offset}", reportingEvent.EventId,
            result.Topic, result.Offset.Value);
    }

    public void Dispose() => _producer.Dispose();
}
