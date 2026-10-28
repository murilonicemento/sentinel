using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TenantsBilling.Application.Services;
using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Events;
using TenantsBilling.Domain.Repositories;
using TenantsBilling.Infrastructure.HostedServices;
using TenantsBilling.Infrastructure.InMemory;
using TenantsBilling.Infrastructure.Messaging.Reliability;

namespace TenantsBilling.UnitTests;

public sealed class TenantUsageConsumerTests
{
    private const string UsageTopic = "tenant-usage-events";
    private const string ReplayTopic = "tenant-usage-events-replay";

    private static readonly ConsumerReliabilityOptions FastRetries = new()
    {
        InitialRetryDelayMilliseconds = 1,
        PauseAfterExhaustedRetries = TimeSpan.FromMilliseconds(1),
        PollTimeout = TimeSpan.FromMilliseconds(10)
    };

    private readonly InMemoryTenantRepository _tenants = new();
    private readonly InMemoryPlanRepository _plans = new();
    private readonly Mock<ITenantEventPublisher> _publisher = new();
    private readonly RecordingSink _sink = new();

    private async Task ConsumeOnceAsync(string topic, string payload, long offset = 11)
    {
        var message = new ConsumeResult<Ignore, string>
        {
            Topic = topic,
            Partition = new Partition(2),
            Offset = new Offset(offset),
            Message = new Message<Ignore, string> { Value = payload }
        };
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = false;
        var consumer = new Mock<IConsumer<Ignore, string>>();
        consumer.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(() =>
        {
            Thread.Sleep(5);
            return Volatile.Read(ref done) ? null! : message;
        });
        consumer.Setup(c => c.Commit(message)).Callback(() =>
        {
            Volatile.Write(ref done, true);
            committed.TrySetResult();
        });

        using var services = new ServiceCollection()
            .AddSingleton<ITenantRepository>(_tenants)
            .AddSingleton<IPlanRepository>(_plans)
            .AddSingleton<ITenantUsageRepository>(new InMemoryTenantUsageRepository())
            .AddSingleton(_publisher.Object)
            .AddScoped<TenantManagementService>()
            .BuildServiceProvider();
        var hosted = new TenantUsageEventConsumerHostedService(consumer.Object, UsageTopic, ReplayTopic,
            new ReliableMessageProcessor(_sink, "tenants-billing-consumer-group", FastRetries, NullLogger.Instance),
            FastRetries, services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TenantUsageEventConsumerHostedService>.Instance);

        await hosted.StartAsync(CancellationToken.None);
        await committed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await hosted.StopAsync(CancellationToken.None);
    }

    private async Task<Tenant> TenantAsync()
    {
        // 100 events per month, so a usage of N events is N% of the plan.
        var plan = new Plan(Guid.NewGuid(), "Basic", 100, 100, 100, 100);
        await _plans.AddAsync(plan);
        var tenant = Tenant.Create("tenant", plan.Id, timeZone: "America/Sao_Paulo");
        await _tenants.AddAsync(tenant);
        return tenant;
    }

    [Fact]
    public async Task InvalidPayload_IsDeadLetteredInTheFormatTheDlqConsumerTreatsAsTerminal()
    {
        await ConsumeOnceAsync(UsageTopic, "{\"tenantId\":\"not-a-guid\"}");

        var deadLetter = TenantDeadLetterConsumerHostedService.ParseDeadLetterEnvelope(Assert.Single(_sink.Payloads));
        Assert.NotNull(deadLetter);
        Assert.Equal(UsageTopic, deadLetter.SourceTopic);
        Assert.Equal((2, 11L), (deadLetter.Partition, deadLetter.Offset));
        Assert.Equal("{\"tenantId\":\"not-a-guid\"}", deadLetter.OriginalPayload);
        Assert.False(TenantDeadLetterConsumerHostedService.ShouldReplay(deadLetter));
    }

    [Fact]
    public async Task UsageWithEventId_PropagatesItToQuotaEvents()
    {
        var tenant = await TenantAsync();
        var eventId = Guid.NewGuid();

        await ConsumeOnceAsync(UsageTopic, JsonSerializer.Serialize(new TenantUsageEvent(tenant.Id, 90, 0, 0, 0, eventId)));

        _publisher.Verify(p => p.PublishAsync("TenantQuotaSoftLimitReached",
            It.Is<object>(o => JsonSerializer.Serialize(o, (JsonSerializerOptions?)null).Contains(eventId.ToString())),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(_sink.Payloads);
    }

    [Fact]
    public async Task UsageWithoutEventId_IsIdentifiedByItsSourcePosition()
    {
        var tenant = await TenantAsync();

        await ConsumeOnceAsync(UsageTopic, JsonSerializer.Serialize(new TenantUsageEvent(tenant.Id, 100, 0, 0, 0)), 42);

        _publisher.Verify(p => p.PublishAsync("TenantQuotaHardLimitReached",
            It.Is<object>(o => JsonSerializer.Serialize(o, (JsonSerializerOptions?)null)
                .Contains("tenant-usage-events:2:42")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReplayedUsage_IsUnwrappedAndRegistered()
    {
        var tenant = await TenantAsync();
        var original = JsonSerializer.Serialize(new TenantUsageEvent(tenant.Id, 95, 0, 0, 0));

        await ConsumeOnceAsync(ReplayTopic, JsonSerializer.Serialize(new
        {
            sourceTopic = UsageTopic,
            originalPayload = original,
            replayRequestedBy = "operator"
        }));

        _publisher.Verify(p => p.PublishAsync("TenantQuotaSoftLimitReached", It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(_sink.Payloads);
    }

    [Fact]
    public async Task UnknownTenant_IsDeadLetteredAsTerminal()
    {
        await ConsumeOnceAsync(UsageTopic, JsonSerializer.Serialize(new TenantUsageEvent(Guid.NewGuid(), 1, 0, 0, 0)));

        var deadLetter = TenantDeadLetterConsumerHostedService.ParseDeadLetterEnvelope(Assert.Single(_sink.Payloads));
        Assert.False(TenantDeadLetterConsumerHostedService.ShouldReplay(deadLetter));
    }

    /// <summary>Serializes envelopes exactly like <c>TenantUsageDeadLetterSink</c> would publish them.</summary>
    private sealed class RecordingSink : IDeadLetterSink
    {
        public List<string> Payloads { get; } = [];

        public Task PublishAsync(DeadLetterEnvelope envelope, Headers? originalHeaders,
            CancellationToken cancellationToken)
        {
            lock (Payloads)
                Payloads.Add(TenantsBilling.Infrastructure.Messaging.TenantUsageDeadLetterSink.Serialize(envelope,
                    FastRetries.MaxProcessingAttempts));
            return Task.CompletedTask;
        }
    }
}
