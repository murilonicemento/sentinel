using AlertOrchestrator.Application.Events;
using AlertOrchestrator.Infrastructure.Messaging.Reliability;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Infrastructure.Messaging.Consumers;

public sealed class RiskUpdatedEventConsumer : KafkaEventConsumer<RiskUpdatedEvent>
{
    public RiskUpdatedEventConsumer(IConsumer<string, string> consumer, string topic, IServiceProvider serviceProvider,
        ReliableMessageProcessor processor, ConsumerReliabilityOptions options,
        ILogger<RiskUpdatedEventConsumer> logger)
        : base(consumer, topic, serviceProvider, processor, options, logger)
    {
    }

    // The handler reads Metadata and parses RiskType; without them it would fail on every attempt.
    protected override bool IsValid(RiskUpdatedEvent @event) =>
        @event.EventId != Guid.Empty && !string.IsNullOrWhiteSpace(@event.Region) &&
        !string.IsNullOrWhiteSpace(@event.RiskType) && @event.Metadata is not null;
}
