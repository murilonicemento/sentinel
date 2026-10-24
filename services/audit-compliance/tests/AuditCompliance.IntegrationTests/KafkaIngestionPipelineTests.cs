using System.Diagnostics.Metrics;
using System.Text.Json;
using AuditCompliance.Application.Interfaces;
using AuditCompliance.Domain.ValueObjects;
using AuditCompliance.Infrastructure.Messaging;
using AuditCompliance.Infrastructure.Observability;
using AuditCompliance.IntegrationTests.Support;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;

namespace AuditCompliance.IntegrationTests;

public class KafkaIngestionPipelineTests : IClassFixture<KafkaPipelineFactory>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly KafkaPipelineFactory _factory;

    public KafkaIngestionPipelineTests(KafkaPipelineFactory factory)
    {
        _factory = factory;
        // Inicia o host (e o consumidor) antes da publicação.
        _ = _factory.Services;
    }

    [Fact]
    public async Task Pipeline_PersistsOnceOnDuplicateDelivery_DeadLettersInvalid_AndAcceptsReplay()
    {
        var tenantId = Guid.NewGuid();
        var duplicatedEvent = Envelope(tenantId, "evt-dup");

        await _factory.PublishAsync(KafkaPipelineFactory.AuditTopic, tenantId.ToString(), duplicatedEvent);
        await _factory.PublishAsync(KafkaPipelineFactory.AuditTopic, tenantId.ToString(), duplicatedEvent);
        await _factory.PublishAsync(KafkaPipelineFactory.AuditTopic, tenantId.ToString(), "{ not-json");
        await _factory.PublishAsync(KafkaPipelineFactory.ReplayTopic, tenantId.ToString(), Envelope(tenantId, "evt-replayed"));

        var deadLetter = await ConsumeDeadLetterAsync();
        Assert.Equal("MALFORMED_MESSAGE", deadLetter.ErrorCode);
        Assert.Equal(KafkaPipelineFactory.AuditTopic, deadLetter.OriginalTopic);
        Assert.Equal("{ not-json", deadLetter.Payload);

        var count = await WaitForRecordCountAsync(tenantId, 2);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Pipeline_WhenStorageIsUnavailable_DoesNotLoseMessageAndPersistsAfterRecovery()
    {
        var tenantId = Guid.NewGuid();
        var repository = _factory.Services.GetRequiredService<IAuditRecordQueryRepository>();
        var filter = AuditRecordFilter.Create(tenantId, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        var failures = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == AuditMetrics.MeterName && instrument.Name == "audit_processing_failures")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) => failures.TrySetResult());
        listener.Start();

        await _factory.PauseStorageAsync();
        try
        {
            await _factory.PublishAsync(KafkaPipelineFactory.AuditTopic, tenantId.ToString(),
                Envelope(tenantId, "evt-during-outage"));

            // O consumidor precisa falhar ao persistir (sem confirmar o offset) antes de o armazenamento voltar.
            await failures.Task.WaitAsync(Timeout);
        }
        finally
        {
            await _factory.ResumeStorageAsync();
        }

        Assert.Equal(1, await WaitForRecordCountAsync(tenantId, 1));
        Assert.Equal(1, await repository.CountAsync(filter, CancellationToken.None));
    }

    private async Task<long> WaitForRecordCountAsync(Guid tenantId, long expected)
    {
        var repository = _factory.Services.GetRequiredService<IAuditRecordQueryRepository>();
        var filter = AuditRecordFilter.Create(tenantId, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var deadline = DateTime.UtcNow + Timeout;
        long count;

        do
        {
            count = await repository.CountAsync(filter, CancellationToken.None);
            if (count >= expected) break;
            await Task.Delay(200);
        } while (DateTime.UtcNow < deadline);

        // Aguarda um ciclo adicional para detectar eventual duplicação tardia.
        await Task.Delay(1000);
        return await repository.CountAsync(filter, CancellationToken.None);
    }

    private async Task<DeadLetterEnvelope> ConsumeDeadLetterAsync()
    {
        using var consumer = new ConsumerBuilder<string?, string>(new ConsumerConfig
        {
            BootstrapServers = _factory.BootstrapServers,
            GroupId = $"dlq-reader-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();

        consumer.Subscribe(KafkaPipelineFactory.DeadLetterTopic);
        try
        {
            var result = await Task.Run(() => consumer.Consume(Timeout));
            Assert.NotNull(result);
            return JsonSerializer.Deserialize<DeadLetterEnvelope>(result.Message.Value)!;
        }
        finally
        {
            consumer.Close();
        }
    }

    private static string Envelope(Guid tenantId, string eventId)
    {
        return JsonSerializer.Serialize(new
        {
            eventId,
            eventType = "TenantUpdated",
            eventVersion = 1,
            sourceService = "tenants-billing",
            occurredAt = DateTimeOffset.UtcNow.AddSeconds(-5).ToString("O"),
            tenantId,
            actor = new { type = "Service", id = "tenants-billing" },
            resource = new { type = "Tenant", id = tenantId.ToString() },
            metadata = new { field = "name" }
        });
    }
}
