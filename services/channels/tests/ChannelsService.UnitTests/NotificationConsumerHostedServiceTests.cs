using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Events;
using ChannelsService.Infrastructure.HostedServices;
using ChannelsService.Infrastructure.Messaging.Reliability;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ChannelsService.UnitTests;

public sealed class NotificationConsumerHostedServiceTests
{
    [Fact]
    public async Task StartAsync_WithUnreachableBroker_DoesNotBlockHostStartup()
    {
        // Nothing listens on port 1, so Consume() blocks exactly as it does on an empty topic.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Kafka:BootstrapServers"] = "127.0.0.1:1" })
            .Build();
        using var services = new ServiceCollection().BuildServiceProvider();
        var consumer = new NotificationConsumerHostedService(
            configuration,
            NullLogger<NotificationConsumerHostedService>.Instance,
            services.GetRequiredService<IServiceScopeFactory>());

        await Task.Run(() => consumer.StartAsync(CancellationToken.None)).WaitAsync(TimeSpan.FromSeconds(5));

        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await consumer.StopAsync(stopTimeout.Token);
    }

    private static readonly ConsumerReliabilityOptions FastRetries = new()
    {
        InitialRetryDelayMilliseconds = 1,
        PauseAfterExhaustedRetries = TimeSpan.FromMilliseconds(1),
        PollTimeout = TimeSpan.FromMilliseconds(10)
    };

    private static async Task<(RecordingSink Sink, Mock<INotificationDispatchService> Dispatch)> ConsumeOnceAsync(
        string payload, Action<Mock<INotificationDispatchService>>? setup = null, string topic = "notification-events")
    {
        var message = new ConsumeResult<Ignore, string>
        {
            Topic = topic,
            Partition = new Partition(0),
            Offset = new Offset(9),
            Message = new Message<Ignore, string> { Value = payload }
        };
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delivered = false;
        var consumer = new Mock<IConsumer<Ignore, string>>();
        consumer.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() =>
        {
            Thread.Sleep(5);
            return Volatile.Read(ref delivered) ? null! : message;
        });
        consumer.Setup(c => c.Commit(message)).Callback(() =>
        {
            Volatile.Write(ref delivered, true);
            committed.TrySetResult();
        });

        var dispatch = new Mock<INotificationDispatchService>();
        dispatch.Setup(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResultDTO { Success = true });
        setup?.Invoke(dispatch);
        using var services = new ServiceCollection().AddSingleton(dispatch.Object).BuildServiceProvider();
        var sink = new RecordingSink();
        var hosted = new NotificationConsumerHostedService(consumer.Object, "notification-events",
            new ReliableMessageProcessor(sink, "channels-service-group", FastRetries, NullLogger.Instance),
            FastRetries, services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<NotificationConsumerHostedService>.Instance);

        await hosted.StartAsync(CancellationToken.None);
        await committed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await hosted.StopAsync(CancellationToken.None);
        return (sink, dispatch);
    }

    // As alert-orchestrator serializes TriggerAlertCommand: PascalCase, the escalation level as a number.
    private static string AlertCommand(Guid windowId, string? tenant) => System.Text.Json.JsonSerializer.Serialize(new
    {
        AlertWindowId = windowId,
        Region = "rj-capital",
        RiskType = "flood",
        RiskScore = 0.91,
        SignalCount = 2,
        SignalSources = new[] { "Sensor", "Sensor" },
        TriggeredAt = DateTime.UtcNow,
        EscalationLevel = 1,
        TenantId = tenant,
        Severity = "high",
        Priority = "urgent"
    });

    [Fact]
    public async Task AlertCommand_IsDeliveredAsANotificationOfTheWindowAndLevel()
    {
        var windowId = Guid.NewGuid();
        NotificationEvent? dispatched = null;

        var (sink, _) = await ConsumeOnceAsync(AlertCommand(windowId, "tenant-a"),
            dispatch => dispatch.Setup(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
                .Callback<NotificationEvent, CancellationToken>((n, _) => dispatched = n)
                .ReturnsAsync(new DeliveryResultDTO { Success = true }),
            topic: "alert-commands");

        Assert.Empty(sink.Envelopes);
        Assert.Equal($"alert:{windowId:N}:1", dispatched!.EventId);
        Assert.Equal("tenant-a", dispatched.TenantId);
        Assert.Equal("rj-capital", dispatched.Metadata!["region"]);
    }

    [Fact]
    public async Task AlertCommandWithoutTenant_IsDeadLetteredAndCommitted()
    {
        var (sink, dispatch) = await ConsumeOnceAsync(AlertCommand(Guid.NewGuid(), null), topic: "alert-commands");

        Assert.Equal(MessageErrorCodes.InvalidPayload, Assert.Single(sink.Envelopes).ErrorCode);
        dispatch.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AlertCommandWithoutWindow_IsDeadLetteredAndCommitted()
    {
        var (sink, dispatch) = await ConsumeOnceAsync(AlertCommand(Guid.Empty, "tenant-a"), topic: "alert-commands");

        Assert.Equal(MessageErrorCodes.InvalidPayload, Assert.Single(sink.Envelopes).ErrorCode);
        dispatch.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NotificationWithoutTenant_IsDeadLetteredAndCommitted()
    {
        var (sink, dispatch) = await ConsumeOnceAsync("{\"eventId\":\"evt-1\"}");

        Assert.Equal(MessageErrorCodes.InvalidPayload, Assert.Single(sink.Envelopes).ErrorCode);
        dispatch.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TransientDispatchFailure_IsRetriedBeforeCommitting()
    {
        var (sink, dispatch) = await ConsumeOnceAsync(
            "{\"eventId\":\"evt-2\",\"tenantId\":\"tenant-a\",\"channels\":[\"Sms\"]}",
            d => d.SetupSequence(x => x.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TimeoutException("database timeout"))
                .ReturnsAsync(new DeliveryResultDTO { Success = true }));

        Assert.Empty(sink.Envelopes);
        dispatch.Verify(x => x.DispatchAsync(It.Is<NotificationEvent>(n => n.TenantId == "tenant-a"),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
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
