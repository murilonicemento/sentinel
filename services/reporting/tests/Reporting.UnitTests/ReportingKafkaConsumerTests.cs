using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Interfaces;
using Reporting.Domain.Entities;
using Reporting.Infrastructure.HostedServices;

namespace Reporting.UnitTests;

public class ReportingKafkaConsumerTests
{
    [Fact]
    public void ParseEnvelopes_WhenPayloadContainsMultipleEvents_ReturnsAll()
    {
        var payload = """
            [
              {"eventId":"evt-1","eventType":"AlertTriggered","tenantId":"tenant-a","region":"north","severity":"High","channel":"sms","riskScore":0.85,"status":"Success","timestamp":"2026-07-07T00:00:00Z","source":"alert-orchestrator"},
              {"eventId":"evt-2","eventType":"NotificationSent","tenantId":"tenant-b","region":"south","severity":"Medium","channel":"push","riskScore":0.42,"status":"Success","timestamp":"2026-07-07T00:01:00Z","source":"channels-service"}
            ]
            """;

        var envelopes = ReportingKafkaConsumerHostedService.ParseEnvelopes(payload);

        Assert.Collection(envelopes,
            envelope => Assert.Equal("evt-1", envelope.EventId),
            envelope => Assert.Equal("evt-2", envelope.EventId));
    }

    [Fact]
    public void ParseEnvelopes_WhenPayloadContainsWrappedEvents_ReturnsAll()
    {
        var payload = """
            {"events":[
              {"eventId":"evt-3","eventType":"RiskUpdated","tenantId":"tenant-c","region":"west","severity":"Low","channel":"email","riskScore":0.33,"status":"Success","timestamp":"2026-07-07T00:02:00Z","source":"risk-evaluation"}
            ]}
            """;

        var envelopes = ReportingKafkaConsumerHostedService.ParseEnvelopes(payload);

        Assert.Single(envelopes);
        Assert.Equal("evt-3", envelopes[0].EventId);
    }

    [Fact]
    public void BuildDeadLetterMessage_IncludesSourceMetadataAndOriginalPayload()
    {
        var payload = """
            {"eventId":"evt-99","eventType":"AlertTriggered","tenantId":"tenant-a"}
            """;

        var deadLetter = ReportingKafkaConsumerHostedService.BuildDeadLetterMessage(
            payload,
            "reporting-events",
            "Processing failed after retries",
            3,
            42,
            99);

        using var document = System.Text.Json.JsonDocument.Parse(deadLetter);
        var root = document.RootElement;

        Assert.Equal("reporting-events", root.GetProperty("sourceTopic").GetString());
        Assert.Equal("Processing failed after retries", root.GetProperty("reason").GetString());
        Assert.Equal(3, root.GetProperty("attempts").GetInt32());
        Assert.Equal(42, root.GetProperty("partition").GetInt32());
        Assert.Equal(99, root.GetProperty("offset").GetInt64());
        Assert.Equal(payload.Trim(), root.GetProperty("originalPayload").GetString());
    }

    [Fact]
    public void BuildReprocessingMessage_IncludesReplayMetadata()
    {
        var payload = """
            {"eventId":"evt-100","eventType":"NotificationSent"}
            """;

        var replay = ReportingKafkaConsumerHostedService.BuildReprocessingMessage(
            payload,
            "reporting-events",
            "manual-replay");

        using var document = System.Text.Json.JsonDocument.Parse(replay);
        var root = document.RootElement;

        Assert.Equal("reporting-events", root.GetProperty("sourceTopic").GetString());
        Assert.Equal("manual-replay", root.GetProperty("reprocessedBy").GetString());
        Assert.True(root.GetProperty("replayRequested").GetBoolean());
        Assert.Equal(payload.Trim(), root.GetProperty("originalPayload").GetString());
    }

    [Fact]
    public void ParseDeadLetterEnvelope_WhenPayloadIsStructured_ReturnsEnvelope()
    {
        var payload = """
            {
              "sourceTopic":"reporting-events",
              "reason":"Processing failed after retries",
              "attempts":3,
              "partition":1,
              "offset":42,
              "deadLetteredAtUtc":"2026-07-10T12:00:00Z",
              "originalPayload":"{\"eventId\":\"evt-99\"}",
              "replayRequested":false,
              "replayRequestedBy":null
            }
            """;

        var deadLetter = ReportingDeadLetterConsumerHostedService.ParseDeadLetterEnvelope(payload);

        Assert.NotNull(deadLetter);
        Assert.Equal("reporting-events", deadLetter!.SourceTopic);
        Assert.Equal("Processing failed after retries", deadLetter.Reason);
        Assert.Equal(3, deadLetter.Attempts);
        Assert.Equal(42, deadLetter.Offset);
        Assert.False(deadLetter.ReplayRequested);
    }

    [Fact]
    public void ShouldReplay_WhenReasonIsRetryableAndOriginalPayloadExists()
    {
        var payload = """
            {
              "sourceTopic":"reporting-events",
              "reason":"Processing failed after retries",
              "attempts":3,
              "partition":1,
              "offset":42,
              "deadLetteredAtUtc":"2026-07-10T12:00:00Z",
              "originalPayload":"{\"eventId\":\"evt-99\"}",
              "replayRequested":false,
              "replayRequestedBy":null
            }
            """;

        var deadLetter = ReportingDeadLetterConsumerHostedService.ParseDeadLetterEnvelope(payload);

        Assert.NotNull(deadLetter);
        Assert.True(ReportingDeadLetterConsumerHostedService.ShouldReplay(deadLetter!));
    }

    [Fact]
    public async Task StartAsync_WithoutKafkaConfiguration_DoesNotThrow()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IReportingEventConsumer, StubReportingEventConsumer>();
        using var provider = services.BuildServiceProvider();

        var sut = new ReportingKafkaConsumerHostedService(
            configuration,
            NullLogger<ReportingKafkaConsumerHostedService>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        await sut.StartAsync(CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);
    }

    private sealed class StubReportingEventConsumer : IReportingEventConsumer
    {
        public Task ProcessAsync(ReportingEventEnvelope envelope, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
