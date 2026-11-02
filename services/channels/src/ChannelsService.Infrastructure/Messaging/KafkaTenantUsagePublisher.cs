using System.Text.Json;
using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Infrastructure.Messaging;

/// <summary>Publishes <c>tenant-usage-events</c> (<c>Kafka:TenantUsageTopic</c>) with the platform's PascalCase.</summary>
public sealed class KafkaTenantUsagePublisher : ITenantUsagePublisher
{
    private readonly IProducer<Null, string> _producer;
    private readonly string _topic;
    private readonly ILogger<KafkaTenantUsagePublisher> _logger;

    public KafkaTenantUsagePublisher(IProducer<Null, string> producer, IConfiguration configuration,
        ILogger<KafkaTenantUsagePublisher> logger)
    {
        _producer = producer;
        _topic = configuration["Kafka:TenantUsageTopic"] ?? "tenant-usage-events";
        _logger = logger;
    }

    public async Task PublishAsync(TenantUsageEventDTO usage, CancellationToken cancellationToken)
    {
        var message = new Message<Null, string> { Value = JsonSerializer.Serialize(usage), Headers = new Headers() };
        using var activity = KafkaTracing.StartPublish(_topic, message.Headers);
        var result = await _producer.ProduceAsync(_topic, message, cancellationToken);
        _logger.LogInformation("Tenant usage {EventId} published to {Topic}@{Offset}", usage.EventId, result.Topic,
            result.Offset.Value);
    }
}
