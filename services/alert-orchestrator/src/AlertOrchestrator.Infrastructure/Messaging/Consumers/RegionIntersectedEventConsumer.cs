using AlertOrchestrator.Application.Events;
using AlertOrchestrator.Infrastructure.Messaging.Reliability;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Infrastructure.Messaging.Consumers;

/// <summary>
/// Deprecated since 2026-11-02, sunset 2027-01-31 (X-24): nothing publishes <c>region-intersected</c> and no producer
/// is planned (ADR-0008). After the sunset, and once the topic and its dead letters are confirmed empty, this consumer,
/// <see cref="RegionIntersectedEvent"/> and its handler are removed. Until then a message is still processed.
/// </summary>
public sealed class RegionIntersectedEventConsumer : KafkaEventConsumer<RegionIntersectedEvent>
{
    public const string Sunset = "2027-01-31";

    public RegionIntersectedEventConsumer(IConsumer<string, string> consumer, string topic,
        IServiceProvider serviceProvider, ReliableMessageProcessor processor, ConsumerReliabilityOptions options,
        ILogger<RegionIntersectedEventConsumer> logger)
        : base(consumer, topic, serviceProvider, processor, options, logger)
    {
        logger.LogWarning("The {Topic} consumer is deprecated and will be removed after {Sunset}", topic, Sunset);
    }

    protected override bool IsValid(RegionIntersectedEvent @event) =>
        @event.EventId != Guid.Empty && !string.IsNullOrWhiteSpace(@event.Region) &&
        !string.IsNullOrWhiteSpace(@event.RiskType);
}
