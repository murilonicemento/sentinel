using System.Diagnostics.Metrics;
using Confluent.Kafka;
using ChannelsService.Infrastructure.Messaging.Reliability;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChannelsService.UnitTests.Messaging;

/// <summary>Consumer outcomes counted by <see cref="MessagingMetrics"/>, the base of the messaging alerts.</summary>
public sealed class MessagingMetricsTests : IDisposable
{
    private readonly string _topic = $"topic-{Guid.NewGuid():N}";
    private readonly List<(string Instrument, string Outcome)> _measurements = [];
    private readonly MeterListener _listener = new();

    public MessagingMetricsTests()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == MessagingMetrics.MeterName) listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            string? topic = null, outcome = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "topic") topic = tag.Value as string;
                if (tag.Key == "outcome") outcome = tag.Value as string;
            }

            if (topic == _topic) lock (_measurements) _measurements.Add((instrument.Name, outcome ?? ""));
        });
        _listener.Start();
    }

    public void Dispose() => _listener.Dispose();

    private ConsumeResult<string, string> Message() => new()
    {
        Topic = _topic,
        Partition = new Partition(0),
        Offset = new Offset(1),
        Message = new Message<string, string> { Key = "k", Value = "{}" }
    };

    private static ReliableMessageProcessor Processor(IDeadLetterSink sink) =>
        new(sink, "tests", new ConsumerReliabilityOptions { InitialRetryDelayMilliseconds = 1 },
            NullLogger.Instance);

    [Fact]
    public async Task Processed_IsCounted()
    {
        await Processor(new Sink()).HandleAsync(Message(), _ => Task.FromResult(MessageHandlingResult.Processed),
            default);

        Assert.Equal([("sentinel_messages_handled", "processed")], _measurements);
    }

    [Fact]
    public async Task Rejected_IsCountedAsDeadLettered()
    {
        await Processor(new Sink()).HandleAsync(Message(),
            _ => Task.FromResult(MessageHandlingResult.Reject("INVALID_PAYLOAD", "bad")), default);

        Assert.Equal([("sentinel_messages_handled", "dead_lettered")], _measurements);
    }

    [Fact]
    public async Task ExhaustedRetries_CountTheRetriesAndNotDurable()
    {
        await Processor(new Sink()).HandleAsync(Message(), _ => throw new TimeoutException("store down"), default);

        Assert.Equal(2, _measurements.Count(m => m.Instrument == "sentinel_message_retries"));
        Assert.Contains(("sentinel_messages_handled", "not_durable"), _measurements);
    }

    [Fact]
    public async Task UnavailableDeadLetter_IsCountedAsNotDurable()
    {
        await Processor(new Sink(fail: true)).HandleAsync(Message(),
            _ => Task.FromResult(MessageHandlingResult.Reject("INVALID_PAYLOAD", "bad")), default);

        Assert.Equal([("sentinel_messages_handled", "not_durable")], _measurements);
    }

    private sealed class Sink(bool fail = false) : IDeadLetterSink
    {
        public Task PublishAsync(DeadLetterEnvelope envelope, Headers? originalHeaders,
            CancellationToken cancellationToken) =>
            fail ? throw new KafkaException(ErrorCode.Local_Transport) : Task.CompletedTask;
    }
}
