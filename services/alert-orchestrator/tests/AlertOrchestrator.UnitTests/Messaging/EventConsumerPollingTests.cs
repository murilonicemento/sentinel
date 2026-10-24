using System.Text.Json;
using AlertOrchestrator.Application.Events;
using AlertOrchestrator.Infrastructure.Messaging.Consumers;
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

    private RiskUpdatedEventConsumer CreateRiskUpdatedConsumer()
    {
        var services = new ServiceCollection().AddSingleton(_mediator.Object).BuildServiceProvider();
        return new RiskUpdatedEventConsumer(_consumer.Object, Topic, services,
            NullLogger<RiskUpdatedEventConsumer>.Instance);
    }

    private RegionIntersectedEventConsumer CreateRegionIntersectedConsumer()
    {
        var services = new ServiceCollection().AddSingleton(_mediator.Object).BuildServiceProvider();
        return new RegionIntersectedEventConsumer(_consumer.Object, "region-intersected", services,
            NullLogger<RegionIntersectedEventConsumer>.Instance);
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
}
