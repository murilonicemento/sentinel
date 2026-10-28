using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using Geospatial.Infrastructure.Messaging;

namespace Geospatial.UnitTests.Messaging;

public sealed class KafkaTracingTests : IDisposable
{
    private readonly ActivityListener _listener = new()
    {
        ShouldListenTo = source => source.Name == KafkaTracing.ActivitySourceName,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
    };

    public KafkaTracingTests() => ActivitySource.AddActivityListener(_listener);

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void Process_ContinuesTheTraceWrittenByPublish()
    {
        var headers = new Headers();
        ActivityTraceId traceId;
        ActivitySpanId publishSpanId;
        using (var publish = KafkaTracing.StartPublish("topic-a", headers))
        {
            traceId = publish!.TraceId;
            publishSpanId = publish.SpanId;
            Assert.Equal(ActivityKind.Producer, publish.Kind);
        }

        Assert.True(headers.TryGetLastBytes("traceparent", out var traceParent));
        Assert.Contains(traceId.ToHexString(), Encoding.UTF8.GetString(traceParent));

        using var process = KafkaTracing.StartProcess("topic-a", headers, partition: 2, offset: 42);

        Assert.Equal(ActivityKind.Consumer, process!.Kind);
        Assert.Equal(traceId, process.TraceId);
        Assert.Equal(publishSpanId, process.ParentSpanId);
        Assert.Equal(42L, process.GetTagItem("messaging.kafka.offset"));
    }

    [Fact]
    public void Publish_ReplacesAnInheritedTraceParent()
    {
        var headers = new Headers
        {
            { "traceparent", Encoding.UTF8.GetBytes("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01") }
        };

        using var publish = KafkaTracing.StartPublish("topic-a", headers);

        Assert.Single(headers, header => header.Key == "traceparent");
        Assert.Contains(publish!.SpanId.ToHexString(),
            Encoding.UTF8.GetString(headers.GetLastBytes("traceparent")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-traceparent")]
    public void Process_WithoutAValidTraceParent_StartsANewTrace(string? traceParent)
    {
        var headers = new Headers();
        if (traceParent is not null) headers.Add("traceparent", Encoding.UTF8.GetBytes(traceParent));

        using var process = KafkaTracing.StartProcess("topic-a", headers, partition: 0, offset: 0);

        Assert.NotNull(process);
        Assert.Equal(default, process.ParentSpanId);
    }
}
