using System.Text.Json;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RiskEvaluation.Application.Contracts;
using RiskEvaluation.Infrastructure.HostedServices;
using RiskEvaluation.Infrastructure.Messaging;
using RiskEvaluation.Infrastructure.Messaging.Reliability;
using Xunit;

namespace RiskEvaluation.UnitTests;

public class RiskEvaluationEventConsumerTests
{
    private static readonly ConsumerReliabilityOptions FastRetries = new()
    {
        InitialRetryDelayMilliseconds = 1,
        PauseAfterExhaustedRetries = TimeSpan.FromMilliseconds(1),
        PollTimeout = TimeSpan.FromMilliseconds(10)
    };

    private readonly InMemoryGuard _guard = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly RecordingSink _sink = new();

    private async Task ConsumeAsync(params ConsumeResult<string?, string>[] messages)
    {
        var queue = new Queue<ConsumeResult<string?, string>>(messages);
        var committed = 0;
        var allCommitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var current = queue.Dequeue();
        var consumer = new Mock<IConsumer<string?, string>>();
        consumer.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() =>
        {
            Thread.Sleep(5);
            lock (queue) return current!;
        });
        consumer.Setup(c => c.Commit(It.IsAny<ConsumeResult<string?, string>>())).Callback(() =>
        {
            lock (queue) current = queue.Count > 0 ? queue.Dequeue() : null;
            if (Interlocked.Increment(ref committed) == messages.Length) allCommitted.TrySetResult();
        });

        using var services = new ServiceCollection().AddSingleton(_mediator.Object).BuildServiceProvider();
        var hosted = new RiskEvaluationEventConsumer(consumer.Object, new RiskEvaluationConsumerTopics(), _guard,
            new ReliableMessageProcessor(_sink, "risk-evaluation-group", FastRetries, NullLogger.Instance),
            FastRetries, services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RiskEvaluationEventConsumer>.Instance);

        await hosted.StartAsync(CancellationToken.None);
        await allCommitted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await hosted.StopAsync(CancellationToken.None);
    }

    private static ConsumeResult<string?, string> Message(string topic, long offset, string value) => new()
    {
        Topic = topic,
        Partition = new Partition(0),
        Offset = new Offset(offset),
        Message = new Message<string?, string> { Value = value }
    };

    private static string Sensor(Guid? eventId) => JsonSerializer.Serialize(new
    {
        EventId = eventId,
        CollectionId = Guid.NewGuid(),
        EventType = "Rainfall",
        Intensity = 30.0,
        Latitude = -23.5,
        Longitude = -46.6,
        CollectedAt = DateTime.UtcNow
    });

    [Fact]
    public async Task SensorEventRedeliveredWithTheSameEventId_IsEvaluatedOnce()
    {
        var payload = Sensor(Guid.NewGuid());

        await ConsumeAsync(Message("climatic-event-detected", 1, payload),
            Message("climatic-event-detected", 2, payload));

        _mediator.Verify(m => m.Publish(It.IsAny<SensorEventDetected>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SensorEventsWithoutEventId_AreIdentifiedByTheirOffset()
    {
        await ConsumeAsync(Message("disaster-event-detected", 1, Sensor(null)),
            Message("disaster-event-detected", 2, Sensor(null)));

        _mediator.Verify(m => m.Publish(It.IsAny<SensorEventDetected>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task FailedEvaluation_ReleasesTheClaimSoTheRetrySucceeds()
    {
        _mediator.SetupSequence(m => m.Publish(It.IsAny<SensorEventDetected>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("mongo timeout"))
            .Returns(Task.CompletedTask);

        await ConsumeAsync(Message("climatic-event-detected", 1, Sensor(Guid.NewGuid())));

        _mediator.Verify(m => m.Publish(It.IsAny<SensorEventDetected>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        Assert.Empty(_sink.Envelopes);
    }

    [Fact]
    public async Task SensorEventWithInvalidCoordinates_IsDeadLettered()
    {
        await ConsumeAsync(Message("climatic-event-detected", 3,
            JsonSerializer.Serialize(new { EventType = "Rainfall", Latitude = 120.0, Longitude = 0.0 })));

        Assert.Equal(MessageErrorCodes.InvalidPayload, Assert.Single(_sink.Envelopes).ErrorCode);
        _mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CatalogPublicationInTheRiskCatalogFormat_IsProcessed()
    {
        // Same serializer options as RiskModelService.PublishCatalogVersionAsync in risk-catalog (camelCase).
        var payload = JsonSerializer.Serialize(
            new { Version = 3, Notes = "n", PublishedAt = DateTime.UtcNow, PublishedBy = Guid.NewGuid() },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Contains("\"version\":3", payload);

        await ConsumeAsync(Message("risk-catalog-published", 4, payload));

        Assert.Empty(_sink.Envelopes);
        _mediator.Verify(m => m.Publish(It.Is<RiskCatalogPublishedEvent>(e => e.Version == 3 && e.Notes == "n"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CatalogPublicationWithoutVersion_IsDeadLettered()
    {
        await ConsumeAsync(Message("risk-catalog-published", 5, "{\"Notes\":\"n\"}"));

        Assert.Equal(MessageErrorCodes.InvalidPayload, Assert.Single(_sink.Envelopes).ErrorCode);
    }

    private sealed class InMemoryGuard : IConsumedMessageGuard
    {
        private readonly Dictionary<string, bool> _claims = new();

        public Task<ConsumedMessageClaim> TryBeginAsync(string messageKey)
        {
            lock (_claims)
            {
                if (_claims.TryGetValue(messageKey, out var done))
                    return Task.FromResult(done ? ConsumedMessageClaim.AlreadyProcessed : ConsumedMessageClaim.InProgress);
                _claims[messageKey] = false;
                return Task.FromResult(ConsumedMessageClaim.Acquired);
            }
        }

        public Task CompleteAsync(string messageKey)
        {
            lock (_claims) _claims[messageKey] = true;
            return Task.CompletedTask;
        }

        public Task AbandonAsync(string messageKey)
        {
            lock (_claims)
                if (_claims.TryGetValue(messageKey, out var done) && !done)
                    _claims.Remove(messageKey);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSink : IDeadLetterSink
    {
        public List<DeadLetterEnvelope> Envelopes { get; } = [];

        public Task PublishAsync(DeadLetterEnvelope envelope, Headers? originalHeaders,
            CancellationToken cancellationToken)
        {
            lock (Envelopes) Envelopes.Add(envelope);
            return Task.CompletedTask;
        }
    }
}
