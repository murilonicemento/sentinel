using ChannelsService.Infrastructure.HostedServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
}
