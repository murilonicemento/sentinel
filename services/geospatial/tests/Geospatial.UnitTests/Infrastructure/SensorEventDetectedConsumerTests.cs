using System.Text.Json;
using Confluent.Kafka;
using Geospatial.Domain.Events;
using Geospatial.Domain.Repositories;
using Geospatial.Infrastructure.Messaging;
using Geospatial.Infrastructure.Messaging.Consumers;
using Geospatial.Infrastructure.Messaging.Reliability;
using Geospatial.Infrastructure.Options;
using Geospatial.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Geospatial.UnitTests.Infrastructure;

public class SensorEventDetectedConsumerTests
{
    [Fact]
    public async Task StartAsync_DoesNotWaitForTheFirstKafkaPoll()
    {
        var kafkaConsumer = new Mock<IConsumer<Null, string>>();
        kafkaConsumer
            .Setup(c => c.Consume(It.IsAny<TimeSpan>()))
            .Returns(() =>
            {
                // Simulates a poll that only returns when its timeout elapses on an empty topic.
                Thread.Sleep(TimeSpan.FromSeconds(3));
                return null!;
            });
        var reliability = new ConsumerReliabilityOptions();
        var consumer = new SensorEventDetectedConsumer(
            kafkaConsumer.Object,
            new ReliableMessageProcessor(Mock.Of<IDeadLetterSink>(), "geospatial-service-consumer", reliability,
                NullLogger.Instance),
            reliability,
            Mock.Of<IGeospatialEventRepository>(),
            Options.Create(new KafkaConsumerOptions
            { TopicName = "sensor-event-detected", DeadLetterTopic = "geospatial-dead-letter" }),
            NullLogger<SensorEventDetectedConsumer>.Instance);

        await Task.Run(() => consumer.StartAsync(CancellationToken.None)).WaitAsync(TimeSpan.FromSeconds(1));

        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await consumer.StopAsync(stopTimeout.Token);

        kafkaConsumer.Verify(c => c.Subscribe(It.Is<IEnumerable<string>>(t => t.Single() == "sensor-event-detected")),
            Times.Once);
    }

    private static readonly ConsumerReliabilityOptions FastRetries = new()
    {
        InitialRetryDelayMilliseconds = 1,
        PauseAfterExhaustedRetries = TimeSpan.FromMilliseconds(1),
        PollTimeout = TimeSpan.FromMilliseconds(10)
    };

    private static async Task<List<string>> ConsumeOnceAsync(string payload, Mock<IGeospatialEventRepository> repository)
    {
        var message = new ConsumeResult<Null, string>
        {
            Topic = "sensor-event-detected",
            Partition = new Partition(1),
            Offset = new Offset(5),
            Message = new Message<Null, string> { Value = payload }
        };
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = false;
        var kafkaConsumer = new Mock<IConsumer<Null, string>>();
        kafkaConsumer.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() =>
        {
            Thread.Sleep(5);
            return Volatile.Read(ref done) ? null! : message;
        });
        kafkaConsumer.Setup(c => c.Commit(message)).Callback(() =>
        {
            Volatile.Write(ref done, true);
            committed.TrySetResult();
        });
        var published = new List<string>();
        var producer = new Mock<IProducer<Null, string>>();
        producer.Setup(p => p.ProduceAsync("geospatial-dead-letter", It.IsAny<Message<Null, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<Null, string>, CancellationToken>((_, m, _) => published.Add(m.Value))
            .ReturnsAsync(new DeliveryResult<Null, string>());

        var consumer = new SensorEventDetectedConsumer(kafkaConsumer.Object,
            new ReliableMessageProcessor(new GeospatialDeadLetterSink(producer.Object, "geospatial-dead-letter"),
                "geospatial-service-consumer", FastRetries, NullLogger.Instance),
            FastRetries, repository.Object,
            Options.Create(new KafkaConsumerOptions
            { TopicName = "sensor-event-detected", DeadLetterTopic = "geospatial-dead-letter" }),
            NullLogger<SensorEventDetectedConsumer>.Instance);

        await consumer.StartAsync(CancellationToken.None);
        await committed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await consumer.StopAsync(CancellationToken.None);
        return published;
    }

    [Fact]
    public async Task ElasticsearchOutage_IsRetriedInsteadOfDeadLettered()
    {
        var eventId = Guid.NewGuid();
        var repository = new Mock<IGeospatialEventRepository>();
        repository.SetupSequence(r => r.IndexAsync(It.IsAny<GeospatialOperationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ElasticsearchUnavailableException(503, null))
            .Returns(Task.CompletedTask);

        var published = await ConsumeOnceAsync(JsonSerializer.Serialize(new
        {
            CollectionId = Guid.NewGuid(),
            EventType = "Rainfall",
            Intensity = 1.0,
            Latitude = -23.5,
            Longitude = -46.6,
            CollectedAt = DateTime.UtcNow,
            EventId = eventId
        }), repository);

        Assert.Empty(published);
        repository.Verify(r => r.IndexAsync(It.Is<GeospatialOperationEvent>(e => e.SourceEventId == eventId),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task InvalidCoordinates_AreDeadLetteredInTheExistingFormat()
    {
        var repository = new Mock<IGeospatialEventRepository>();

        var published = await ConsumeOnceAsync("{\"Latitude\":120,\"Longitude\":0}", repository);

        using var envelope = JsonDocument.Parse(Assert.Single(published));
        Assert.Equal("sensor-event-detected", envelope.RootElement.GetProperty("OriginalTopic").GetString());
        Assert.Equal(5, envelope.RootElement.GetProperty("OriginalOffset").GetInt64());
        Assert.Equal(MessageErrorCodes.InvalidPayload, envelope.RootElement.GetProperty("ErrorCode").GetString());
        repository.VerifyNoOtherCalls();
    }
}
