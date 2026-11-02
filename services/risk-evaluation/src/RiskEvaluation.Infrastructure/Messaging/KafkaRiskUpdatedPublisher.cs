using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.Contracts;
using RiskEvaluation.Application.Interfaces.Messaging;

namespace RiskEvaluation.Infrastructure.Messaging;

/// <summary>
/// Publishes <c>risk-updated</c> keyed by tenant and region, so the signals of one alert window stay in one partition
/// and reach alert-orchestrator in order.
/// </summary>
public sealed class KafkaRiskUpdatedPublisher : IRiskUpdatedPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;
    private readonly ILogger<KafkaRiskUpdatedPublisher> _logger;

    public KafkaRiskUpdatedPublisher(string bootstrapServers, string topic, ILogger<KafkaRiskUpdatedPublisher> logger)
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

    public async Task PublishAsync(RiskUpdatedEvent riskUpdated, CancellationToken cancellationToken = default)
    {
        var message = new Message<string, string>
        {
            Key = $"{riskUpdated.TenantId}:{riskUpdated.Region}",
            Value = JsonSerializer.Serialize(riskUpdated),
            Headers = new Headers()
        };

        using var activity = KafkaTracing.StartPublish(_topic, message.Headers);
        var result = await _producer.ProduceAsync(_topic, message, cancellationToken);
        _logger.LogInformation("risk-updated {EventId} for region {Region} published to {Topic}@{Offset}",
            riskUpdated.EventId, riskUpdated.Region, result.Topic, result.Offset.Value);
    }

    public void Dispose() => _producer.Dispose();
}
