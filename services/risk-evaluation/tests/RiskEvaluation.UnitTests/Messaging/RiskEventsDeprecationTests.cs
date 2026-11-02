using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RiskEvaluation.Application.Interfaces.Messaging;
using RiskEvaluation.Infrastructure;
using RiskEvaluation.Infrastructure.Messaging;

namespace RiskEvaluation.UnitTests.Messaging;

/// <summary>risk-events is deprecated (X-24): published by default until its sunset, off with a setting.</summary>
public class RiskEventsDeprecationTests
{
    private static ServiceDescriptor EventPublisherRegistration(string? publishRiskEvents)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:MongoDb"] = "mongodb://localhost:27017",
            ["ConnectionStrings:Redis"] = "localhost:6379",
            ["Services:RiskCatalog:BaseUrl"] = "http://risk-catalog:8080",
            ["Kafka:BootstrapServers"] = "localhost:9092",
            ["Kafka:Topic"] = "risk-events"
        };
        if (publishRiskEvents is not null) settings["Kafka:PublishRiskEvents"] = publishRiskEvents;

        var services = new ServiceCollection().AddInfrastructureServiceCollection(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        return services.Single(d => d.ServiceType == typeof(IEventPublisher));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("true")]
    public void RiskEvents_ArePublishedByDefault(string? setting)
    {
        var registration = EventPublisherRegistration(setting);

        Assert.NotNull(registration.ImplementationFactory);
    }

    [Fact]
    public void RiskEvents_StopWhenDisabled()
    {
        Assert.Equal(typeof(DisabledEventPublisher), EventPublisherRegistration("false").ImplementationType);
    }

    [Fact]
    public async Task DisabledPublisher_PublishesNothingAndDoesNotFail()
    {
        await new DisabledEventPublisher(NullLogger<DisabledEventPublisher>.Instance).PublishAsync(new { Score = 0.9 });
    }
}
