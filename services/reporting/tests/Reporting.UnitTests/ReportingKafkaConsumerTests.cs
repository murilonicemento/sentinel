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
