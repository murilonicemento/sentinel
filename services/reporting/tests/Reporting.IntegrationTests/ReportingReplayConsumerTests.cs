using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Interfaces;
using Reporting.Domain.Entities;
using Reporting.Domain.Interfaces;
using Reporting.Infrastructure.HostedServices;
using Testcontainers.Kafka;

namespace Reporting.IntegrationTests;

/// <summary>
/// The reporting consumer reads the replay topic on a real Kafka (REP-09).
/// </summary>
public sealed class ReportingReplayConsumerTests : IClassFixture<KafkaFixture>, IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly KafkaFixture _kafka;

    // One Kafka for the class; each test has its own topics, so one test never reads the other's messages.
    private readonly string _suffix = Guid.NewGuid().ToString("N");
    private string MainTopic => $"reporting-events-{_suffix}";
    private string ReplayTopic => $"reporting-events-replay-{_suffix}";
    private string DeadLetterTopic => $"reporting-events-dead-letter-{_suffix}";

    public ReportingReplayConsumerTests(KafkaFixture kafka)
    {
        _kafka = kafka;
    }

    public async Task InitializeAsync()
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = _kafka.BootstrapServers })
            .Build();
        await admin.CreateTopicsAsync(new[] { MainTopic, ReplayTopic, DeadLetterTopic }.Select(topic =>
            new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReplayedPayload_IsProcessed()
    {
        var consumer = new RecordingConsumer();
        await using var host = await StartAsync(consumer);

        await PublishAsync(ReplayTopic, ReportingKafkaConsumerHostedService.BuildReprocessingMessage(
            Payload("evt-replayed"), MainTopic, "admin"));

        var processed = await consumer.NextAsync(Timeout);
        Assert.Equal("evt-replayed", processed.EventId);
        Assert.Equal("tenant-a", processed.TenantId);
        var audit = await host.Audits.NextAsync(Timeout);
        Assert.Equal("evt-replayed", audit.EventId);
        Assert.Equal(ReplayTopic, audit.SourceTopic);
        Assert.Equal("Success", audit.Status);
    }

    [Fact]
    public async Task ReplayThatFailsAgain_IsDeadLetteredAsTerminal()
    {
        await using var host = await StartAsync(new RecordingConsumer(fail: true));
        var payload = Payload("evt-broken");

        await PublishAsync(ReplayTopic, ReportingKafkaConsumerHostedService.BuildReprocessingMessage(payload, MainTopic, "admin"));

        var deadLetter = ReportingDeadLetterConsumerHostedService.ParseDeadLetterEnvelope(ReadDeadLetter());
        Assert.NotNull(deadLetter);
        Assert.Equal(ReplayTopic, deadLetter!.SourceTopic);
        Assert.Equal(payload.Trim(), deadLetter.OriginalPayload);
        Assert.False(ReportingDeadLetterConsumerHostedService.ShouldReplay(deadLetter, ReplayTopic));
    }

    private static string Payload(string eventId) => $$"""
        {"eventId":"{{eventId}}","eventType":"alert","tenantId":"tenant-a","region":"sp","severity":"high","channel":"sms","riskScore":0.9,"status":"sent","timestamp":"2026-11-01T12:00:00Z","source":"tests"}
        """;

    private async Task<RunningConsumer> StartAsync(RecordingConsumer eventConsumer)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kafka:BootstrapServers"] = _kafka.BootstrapServers,
            ["Kafka:Topic"] = MainTopic,
            ["Kafka:ReplayTopic"] = ReplayTopic,
            ["Kafka:DeadLetterTopic"] = DeadLetterTopic,
            ["Kafka:ConsumerGroupId"] = $"reporting-tests-{Guid.NewGuid():N}",
            ["Kafka:ProcessingRetryDelaySeconds"] = "1",
            ["Kafka:ProcessingRetryMaxDelaySeconds"] = "1"
        }).Build();

        var audits = new RecordingAuditRepository();
        var services = new ServiceCollection();
        services.AddSingleton<IReportingEventConsumer>(eventConsumer);
        services.AddSingleton<IReportingAuditRepository>(audits);
        var provider = services.BuildServiceProvider();

        var hostedService = new ReportingKafkaConsumerHostedService(configuration,
            NullLogger<ReportingKafkaConsumerHostedService>.Instance, provider.GetRequiredService<IServiceScopeFactory>());
        await hostedService.StartAsync(CancellationToken.None);
        return new RunningConsumer(hostedService, provider, audits);
    }

    private async Task PublishAsync(string topic, string value)
    {
        using var producer = new ProducerBuilder<Null, string>(new ProducerConfig
        {
            BootstrapServers = _kafka.BootstrapServers,
            Acks = Acks.All
        }).Build();
        await producer.ProduceAsync(topic, new Message<Null, string> { Value = value });
    }

    private string ReadDeadLetter()
    {
        using var reader = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = _kafka.BootstrapServers,
            GroupId = $"reporting-dlq-reader-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();
        reader.Subscribe(DeadLetterTopic);

        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            var result = reader.Consume(TimeSpan.FromSeconds(1));
            if (result?.Message?.Value is { } value)
            {
                return value;
            }
        }

        throw new TimeoutException($"No message on {DeadLetterTopic}.");
    }

    private sealed class RecordingConsumer(bool fail = false) : IReportingEventConsumer
    {
        private readonly TaskCompletionSource<ReportingEventEnvelope> _processed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ProcessAsync(ReportingEventEnvelope envelope, CancellationToken cancellationToken = default)
        {
            if (fail)
            {
                throw new InvalidOperationException("Processing failed.");
            }

            _processed.TrySetResult(envelope);
            return Task.CompletedTask;
        }

        public Task<ReportingEventEnvelope> NextAsync(TimeSpan timeout) => _processed.Task.WaitAsync(timeout);
    }

    private sealed class RecordingAuditRepository : IReportingAuditRepository
    {
        private readonly TaskCompletionSource<ReportingEventProcessingAudit> _saved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SaveAsync(ReportingEventProcessingAudit audit, CancellationToken cancellationToken = default)
        {
            _saved.TrySetResult(audit);
            return Task.CompletedTask;
        }

        public Task<ReportingEventProcessingAudit> NextAsync(TimeSpan timeout) => _saved.Task.WaitAsync(timeout);
    }

    private sealed class RunningConsumer(
        ReportingKafkaConsumerHostedService hostedService,
        ServiceProvider provider,
        RecordingAuditRepository audits) : IAsyncDisposable
    {
        public RecordingAuditRepository Audits => audits;

        public async ValueTask DisposeAsync()
        {
            await hostedService.StopAsync(CancellationToken.None);
            hostedService.Dispose();
            await provider.DisposeAsync();
        }
    }
}

/// <summary>A Kafka shared by the tests of a class.</summary>
public sealed class KafkaFixture : IAsyncLifetime
{
    private readonly KafkaContainer _container = new KafkaBuilder("confluentinc/cp-kafka:7.6.0").Build();

    public string BootstrapServers => _container.GetBootstrapAddress();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
