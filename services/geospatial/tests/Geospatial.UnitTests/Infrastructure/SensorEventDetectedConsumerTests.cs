using Confluent.Kafka;
using Geospatial.Domain.Repositories;
using Geospatial.Infrastructure.Messaging.Consumers;
using Geospatial.Infrastructure.Options;
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
        var consumer = new SensorEventDetectedConsumer(
            kafkaConsumer.Object,
            Mock.Of<IProducer<Null, string>>(),
            Mock.Of<IGeospatialEventRepository>(),
            Options.Create(new KafkaConsumerOptions
            { TopicName = "sensor-event-detected", DeadLetterTopic = "geospatial-dead-letter" }),
            NullLogger<SensorEventDetectedConsumer>.Instance);

        await Task.Run(() => consumer.StartAsync(CancellationToken.None)).WaitAsync(TimeSpan.FromSeconds(1));

        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await consumer.StopAsync(stopTimeout.Token);

        kafkaConsumer.Verify(c => c.Subscribe("sensor-event-detected"), Times.Once);
    }
}
