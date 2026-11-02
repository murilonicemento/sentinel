namespace Geospatial.Infrastructure.Options;

public sealed class KafkaConsumerOptions
{
    /// <summary>
    /// Topics carrying <c>SensorEventDetected</c>: ingestion's <c>disaster-event-detected</c> and
    /// <c>climatic-event-detected</c> (X-24).
    /// </summary>
    public string[] Topics { get; init; } = [];

    /// <summary>
    /// Deprecated single topic, read only when <see cref="Topics"/> is empty. It used to be
    /// <c>sensor-event-detected</c>, which nothing publishes.
    /// </summary>
    public string? TopicName { get; init; }

    public string DeadLetterTopic { get; init; } = default!;

    public IReadOnlyCollection<string> SubscribedTopics =>
        Topics.Length > 0
            ? Topics
            : string.IsNullOrWhiteSpace(TopicName) ? [] : [TopicName];
}
