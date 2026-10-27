using System.Text.Json;
using AlertOrchestrator.Application.Events;
using AlertOrchestrator.Application.Interfaces.Messaging;
using AlertOrchestrator.Infrastructure.Messaging.Consumers;
using AlertOrchestrator.Infrastructure.Messaging.Reliability;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AlertOrchestrator.UnitTests.Messaging;

public class EventConsumerPollingTests
{
    private const string Topic = "risk-updated";
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private readonly Mock<IConsumer<string, string>> _consumer = new();
    private readonly Mock<IMediator> _mediator = new();

    private static readonly ConsumerReliabilityOptions FastRetries = new()
    {
        InitialRetryDelayMilliseconds = 1,
        PauseAfterExhaustedRetries = TimeSpan.FromMilliseconds(1),
        PollTimeout = TimeSpan.FromMilliseconds(10)
    };

    private readonly RecordingDeadLetterSink _deadLetters = new();

    private ReliableMessageProcessor Processor() =>
        new(_deadLetters, "alert-orchestrator-group", FastRetries, NullLogger.Instance);

    private RiskUpdatedEventConsumer CreateRiskUpdatedConsumer()
    {
        var services = new ServiceCollection().AddSingleton(_mediator.Object).BuildServiceProvider();
        return new RiskUpdatedEventConsumer(_consumer.Object, Topic, services, Processor(), FastRetries,
            NullLogger<RiskUpdatedEventConsumer>.Instance);
    }

    private RegionIntersectedEventConsumer CreateRegionIntersectedConsumer()
    {
        var services = new ServiceCollection().AddSingleton(_mediator.Object).BuildServiceProvider();
        return new RegionIntersectedEventConsumer(_consumer.Object, "region-intersected", services, Processor(),
            FastRetries, NullLogger<RegionIntersectedEventConsumer>.Instance);
    }

    private static ConsumeResult<string, string> MessageAt(long offset, string value) => new()
    {
        Topic = Topic,
        Partition = new Partition(0),
        Offset = new Offset(offset),
        Message = new Message<string, string> { Key = "k", Value = value }
    };

    private static string ValidRiskUpdated() => JsonSerializer.Serialize(new RiskUpdatedEvent(Guid.NewGuid(),
        "region-a", "Flood", 0.8, "tenant-a", DateTime.UtcNow, new Dictionary<string, string>()));

    /// <summary>
    /// Delivers <paramref name="message"/> on every poll until it is committed, like Kafka after a seek back to an
    /// uncommitted offset.
    /// </summary>
    private void DeliverRepeatedly(ConsumeResult<string, string> message)
    {
        var committed = false;
        _consumer.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() =>
        {
            Thread.Sleep(5);
            return Volatile.Read(ref committed) ? null! : message;
        });
        _consumer.Setup(c => c.Commit(message)).Callback(() => Volatile.Write(ref committed, true));
    }

    private async Task RunUntilAsync(Task condition)
    {
        var consumer = CreateRiskUpdatedConsumer();
        await consumer.StartAsync(CancellationToken.None);
        await condition.WaitAsync(WaitTimeout);
        await consumer.StopAsync(CancellationToken.None);
    }

    private TaskCompletionSource SetupEmptyPolls(int pollsToObserve)
    {
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var polls = 0;

        _consumer
            .Setup(c => c.Consume(It.IsAny<TimeSpan>()))
            .Returns(() =>
            {
                Thread.Sleep(10);
                if (Interlocked.Increment(ref polls) >= pollsToObserve) observed.TrySetResult();
                return null!;
            });

        return observed;
    }

    [Fact]
    public async Task RiskUpdatedConsumer_EmptyPolls_KeepsPollingUntilStopped()
    {
        var observed = SetupEmptyPolls(pollsToObserve: 3);
        var consumer = CreateRiskUpdatedConsumer();

        // A poll loop running synchronously inside StartAsync would block host startup indefinitely.
        await Task.Run(() => consumer.StartAsync(CancellationToken.None)).WaitAsync(WaitTimeout);

        await observed.Task.WaitAsync(WaitTimeout);
        await consumer.StopAsync(CancellationToken.None);

        _consumer.Verify(c => c.Consume(It.IsAny<TimeSpan>()), Times.AtLeast(3));
        _consumer.Verify(c => c.Close(), Times.Once);
    }

    [Fact]
    public async Task RegionIntersectedConsumer_EmptyPolls_KeepsPollingUntilStopped()
    {
        var observed = SetupEmptyPolls(pollsToObserve: 3);
        var consumer = CreateRegionIntersectedConsumer();

        // A poll loop running synchronously inside StartAsync would block host startup indefinitely.
        await Task.Run(() => consumer.StartAsync(CancellationToken.None)).WaitAsync(WaitTimeout);

        await observed.Task.WaitAsync(WaitTimeout);
        await consumer.StopAsync(CancellationToken.None);

        _consumer.Verify(c => c.Consume(It.IsAny<TimeSpan>()), Times.AtLeast(3));
        _consumer.Verify(c => c.Close(), Times.Once);
    }

    [Fact]
    public async Task RiskUpdatedConsumer_MessageAfterEmptyPoll_IsPublishedAndCommitted()
    {
        var riskUpdated = new RiskUpdatedEvent(Guid.NewGuid(), "region-a", "Flood", 0.8, "tenant-a",
            DateTime.UtcNow, new Dictionary<string, string>());
        var message = new ConsumeResult<string, string>
        {
            Topic = Topic,
            Message = new Message<string, string> { Key = "k", Value = JsonSerializer.Serialize(riskUpdated) }
        };
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var polls = 0;

        _consumer
            .Setup(c => c.Consume(It.IsAny<TimeSpan>()))
            .Returns(() =>
            {
                Thread.Sleep(10);
                return Interlocked.Increment(ref polls) == 2 ? message : null!;
            });
        _consumer
            .Setup(c => c.Commit(message))
            .Callback(() => committed.TrySetResult());

        var consumer = CreateRiskUpdatedConsumer();
        await consumer.StartAsync(CancellationToken.None);

        await committed.Task.WaitAsync(WaitTimeout);
        await consumer.StopAsync(CancellationToken.None);

        _mediator.Verify(
            m => m.Publish(It.Is<RiskUpdatedEvent>(e => e.EventId == riskUpdated.EventId), It.IsAny<CancellationToken>()),
            Times.Once);
        _consumer.Verify(c => c.Commit(message), Times.Once);
    }

    [Fact]
    public async Task InvalidPayload_IsDeadLetteredAndCommitted()
    {
        var message = MessageAt(7, "{not json");
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DeliverRepeatedly(message);
        _consumer.Setup(c => c.Commit(message)).Callback(() =>
        {
            committed.TrySetResult();
            _consumer.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() => null!);
        });

        await RunUntilAsync(committed.Task);

        var envelope = Assert.Single(_deadLetters.Envelopes.Distinct());
        Assert.Equal(MessageErrorCodes.InvalidPayload, envelope.ErrorCode);
        Assert.Equal((Topic, 0, 7L), (envelope.OriginalTopic, envelope.Partition, envelope.Offset));
        Assert.Equal("{not json", envelope.Payload);
        _mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TransientFailure_IsRetriedThenCommitted()
    {
        var message = MessageAt(1, ValidRiskUpdated());
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DeliverRepeatedly(message);
        _consumer.Setup(c => c.Commit(message)).Callback(() =>
        {
            committed.TrySetResult();
            _consumer.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() => null!);
        });
        _mediator.SetupSequence(m => m.Publish(It.IsAny<RiskUpdatedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("redis timeout"))
            .Returns(Task.CompletedTask);

        await RunUntilAsync(committed.Task);

        _mediator.Verify(m => m.Publish(It.IsAny<RiskUpdatedEvent>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
        Assert.Empty(_deadLetters.Envelopes);
    }

    [Fact]
    public async Task PersistentTransientFailure_IsNeitherCommittedNorDeadLettered()
    {
        var message = MessageAt(3, ValidRiskUpdated());
        var sought = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DeliverRepeatedly(message);
        _consumer.Setup(c => c.Seek(message.TopicPartitionOffset)).Callback(() => sought.TrySetResult());
        _mediator.Setup(m => m.Publish(It.IsAny<RiskUpdatedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MessageInProgressException(Guid.NewGuid()));

        await RunUntilAsync(sought.Task);

        _consumer.Verify(c => c.Commit(It.IsAny<ConsumeResult<string, string>>()), Times.Never);
        Assert.Empty(_deadLetters.Envelopes);
        _mediator.Verify(m => m.Publish(It.IsAny<RiskUpdatedEvent>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(FastRetries.MaxProcessingAttempts));
    }

    [Fact]
    public async Task PermanentFailure_IsDeadLetteredAsUnexpectedAndCommitted()
    {
        var message = MessageAt(4, ValidRiskUpdated());
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DeliverRepeatedly(message);
        _consumer.Setup(c => c.Commit(message)).Callback(() =>
        {
            committed.TrySetResult();
            _consumer.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() => null!);
        });
        _mediator.Setup(m => m.Publish(It.IsAny<RiskUpdatedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Unknown risk type"));

        await RunUntilAsync(committed.Task);

        Assert.Equal(MessageErrorCodes.UnexpectedProcessingError, _deadLetters.Envelopes.First().ErrorCode);
        Assert.DoesNotContain("Unknown risk type", _deadLetters.Envelopes.First().ErrorReason);
        _mediator.Verify(m => m.Publish(It.IsAny<RiskUpdatedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeadLetterFailure_KeepsTheMessageUncommitted()
    {
        var message = MessageAt(5, "{not json");
        var sought = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DeliverRepeatedly(message);
        _deadLetters.Fail = true;
        _consumer.Setup(c => c.Seek(message.TopicPartitionOffset)).Callback(() => sought.TrySetResult());

        await RunUntilAsync(sought.Task);

        _consumer.Verify(c => c.Commit(It.IsAny<ConsumeResult<string, string>>()), Times.Never);
    }

    private sealed class RecordingDeadLetterSink : IDeadLetterSink
    {
        private readonly List<DeadLetterEnvelope> _envelopes = [];

        public bool Fail { get; set; }

        public IReadOnlyList<DeadLetterEnvelope> Envelopes
        {
            get
            {
                lock (_envelopes) return _envelopes.ToList();
            }
        }

        public Task PublishAsync(DeadLetterEnvelope envelope, Headers? originalHeaders,
            CancellationToken cancellationToken)
        {
            if (Fail) throw new KafkaException(ErrorCode.Local_MsgTimedOut);
            lock (_envelopes) _envelopes.Add(envelope);
            return Task.CompletedTask;
        }
    }
}
