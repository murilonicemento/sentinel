using System.Diagnostics;
using System.Text;
using Confluent.Kafka;

namespace RiskEvaluation.Infrastructure.Messaging;

/// <summary>
/// W3C trace context over Kafka headers (ADR-0004): producers write <c>traceparent</c>/<c>tracestate</c>, consumers
/// continue the trace from them. Spans come from the <c>Sentinel.Messaging</c> source, which the Api exports to Tempo.
/// Each service keeps its own copy (no shared library yet).
/// </summary>
public static class KafkaTracing
{
    public const string ActivitySourceName = "Sentinel.Messaging";

    private static readonly ActivitySource Source = new(ActivitySourceName);

    /// <summary>Starts a producer span and writes its context into <paramref name="headers"/>.</summary>
    public static Activity? StartPublish(string topic, Headers headers)
    {
        var activity = Source.StartActivity($"{topic} publish", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", topic);
        activity?.SetTag("messaging.operation.type", "send");

        var context = activity ?? Activity.Current;
        if (context is not null)
            DistributedContextPropagator.Current.Inject(context, headers, static (carrier, key, value) =>
            {
                var target = (Headers)carrier!;
                target.Remove(key);
                target.Add(key, Encoding.UTF8.GetBytes(value));
            });
        return activity;
    }

    /// <summary>Starts a consumer span whose parent is the context found in the message headers, if any.</summary>
    public static Activity? StartProcess(string topic, Headers? headers, int partition, long offset)
    {
        ActivityContext parent = default;
        if (headers is not null)
        {
            DistributedContextPropagator.Current.ExtractTraceIdAndState(headers, static (object? carrier,
                string key, out string? value, out IEnumerable<string>? values) =>
            {
                values = null;
                value = ((Headers)carrier!).TryGetLastBytes(key, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;
            }, out var traceParent, out var traceState);
            if (traceParent is not null)
                ActivityContext.TryParse(traceParent, traceState, isRemote: true, out parent);
        }

        var activity = Source.StartActivity($"{topic} process", ActivityKind.Consumer, parent);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", topic);
        activity?.SetTag("messaging.operation.type", "process");
        activity?.SetTag("messaging.destination.partition.id", partition.ToString());
        activity?.SetTag("messaging.kafka.offset", offset);
        return activity;
    }
}
