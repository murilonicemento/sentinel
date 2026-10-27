using AlertOrchestrator.Application.Events;
using AlertOrchestrator.Infrastructure.Messaging.Reliability;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Infrastructure.Messaging.Consumers;

public sealed class RegionIntersectedEventConsumer : KafkaEventConsumer<RegionIntersectedEvent>
{
    public RegionIntersectedEventConsumer(IConsumer<string, string> consumer, string topic,
        IServiceProvider serviceProvider, ReliableMessageProcessor processor, ConsumerReliabilityOptions options,
        ILogger<RegionIntersectedEventConsumer> logger)
        : base(consumer, topic, serviceProvider, processor, options, logger)
    {
    }

    protected override bool IsValid(RegionIntersectedEvent @event) =>
        @event.EventId != Guid.Empty && !string.IsNullOrWhiteSpace(@event.Region) &&
        !string.IsNullOrWhiteSpace(@event.RiskType);
}
