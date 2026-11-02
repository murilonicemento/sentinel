using System.Text.Json;
using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Infrastructure.Messaging;

/// <summary>Publishes <c>reporting-events</c> (<c>Kafka:ReportingTopic</c>) with the platform's PascalCase.</summary>
public sealed class KafkaReportingEventPublisher : IReportingEventPublisher
{
    private readonly IProducer<Null, string> _producer;
    private readonly string _topic;
    private readonly ILogger<KafkaReportingEventPublisher> _logger;

    public KafkaReportingEventPublisher(IProducer<Null, string> producer, IConfiguration configuration,
        ILogger<KafkaReportingEventPublisher> logger)
    {
        _producer = producer;
        _topic = configuration["Kafka:ReportingTopic"] ?? "reporting-events";
        _logger = logger;
    }

    public async Task PublishAsync(ReportingEventEnvelopeDTO reportingEvent, CancellationToken cancellationToken)
    {
        var message = new Message<Null, string>
        {
            Value = JsonSerializer.Serialize(reportingEvent),
            Headers = new Headers()
        };
        using var activity = KafkaTracing.StartPublish(_topic, message.Headers);
        var result = await _producer.ProduceAsync(_topic, message, cancellationToken);
        _logger.LogInformation("Reporting event {EventId} published to {Topic}@{Offset}", reportingEvent.EventId,
            result.Topic, result.Offset.Value);
    }
}
