using AlertOrchestrator.Application.Events;
using AlertOrchestrator.Application.Interfaces.Services;
using AlertOrchestrator.IntegrationTests.Support;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AlertOrchestrator.IntegrationTests;

public class CompositionTests : IClassFixture<AlertOrchestratorApiFactory>
{
    private readonly AlertOrchestratorApiFactory _factory;

    public CompositionTests(AlertOrchestratorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void TenantBillingGateway_ResolvesFromContainer()
    {
        using var scope = _factory.Services.CreateScope();

        var gateway = scope.ServiceProvider.GetRequiredService<ITenantBillingGateway>();

        Assert.NotNull(gateway);
    }

    [Fact]
    public void EventHandlersConsumedFromKafka_ResolveFromContainer()
    {
        using var scope = _factory.Services.CreateScope();

        Assert.NotEmpty(scope.ServiceProvider.GetServices<INotificationHandler<RiskUpdatedEvent>>());
        Assert.NotEmpty(scope.ServiceProvider.GetServices<INotificationHandler<RegionIntersectedEvent>>());
    }
}
