using System.Diagnostics.Metrics;

namespace Reporting.Infrastructure.Messaging;

/// <summary>
/// Consumer outcomes (meter <see cref="MeterName"/>, exported by the Api): every consumed message ends processed,
/// dead-lettered or not durable (retries exhausted or dead-letter unavailable: the consumer seeks back and pauses).
/// Labels are the topic and outcome only. Each consuming service keeps its own copy.
/// </summary>
public static class MessagingMetrics
{
    public const string MeterName = "Sentinel.Messaging";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Handled = Meter.CreateCounter<long>("sentinel_messages_handled",
        description: "Consumed messages per topic and outcome (processed, dead_lettered, not_durable)");

    private static readonly Counter<long> Retries = Meter.CreateCounter<long>("sentinel_message_retries",
        description: "Retries after transient failures per topic");

    public static void Processed(string topic) => Handled.Add(1, new("topic", topic), new("outcome", "processed"));

    public static void DeadLettered(string topic) =>
        Handled.Add(1, new("topic", topic), new("outcome", "dead_lettered"));

    public static void NotDurable(string topic) => Handled.Add(1, new("topic", topic), new("outcome", "not_durable"));

    public static void Retried(string topic) => Retries.Add(1, new KeyValuePair<string, object?>("topic", topic));
}
