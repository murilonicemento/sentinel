namespace Geospatial.Infrastructure.Options;

public sealed class KafkaConsumerOptions
{
    public string TopicName { get; init; } = default!;
    public string DeadLetterTopic { get; init; } = default!;
}