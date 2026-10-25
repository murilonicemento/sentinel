using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Infrastructure.HostedServices;

namespace Reporting.UnitTests;

public class ReportingConsumerStartupTests
{
    // Nothing listens on port 1, so Consume() blocks exactly as it does on an empty topic.
    private static readonly IConfiguration UnreachableBroker = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Kafka:BootstrapServers"] = "127.0.0.1:1" })
        .Build();

    [Fact]
    public async Task KafkaConsumer_StartAsync_WithUnreachableBroker_DoesNotBlockHostStartup()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var consumer = new ReportingKafkaConsumerHostedService(
            UnreachableBroker,
            NullLogger<ReportingKafkaConsumerHostedService>.Instance,
            services.GetRequiredService<IServiceScopeFactory>());

        await AssertStartupDoesNotBlock(consumer);
    }

    [Fact]
    public async Task DeadLetterConsumer_StartAsync_WithUnreachableBroker_DoesNotBlockHostStartup()
    {
        var consumer = new ReportingDeadLetterConsumerHostedService(
            UnreachableBroker,
            NullLogger<ReportingDeadLetterConsumerHostedService>.Instance);

        await AssertStartupDoesNotBlock(consumer);
    }

    private static async Task AssertStartupDoesNotBlock(IHostedService consumer)
    {
        await Task.Run(() => consumer.StartAsync(CancellationToken.None)).WaitAsync(TimeSpan.FromSeconds(5));

        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await consumer.StopAsync(stopTimeout.Token);
    }
}
