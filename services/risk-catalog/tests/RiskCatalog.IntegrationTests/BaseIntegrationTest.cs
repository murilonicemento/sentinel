using Microsoft.Extensions.DependencyInjection;

namespace RiskCatalog.IntegrationTests;

public abstract class BaseIntegrationTest : IClassFixture<RiskCatalogWebApplicationFactory>
{
    private readonly IServiceScope _scope;

    protected BaseIntegrationTest(RiskCatalogWebApplicationFactory factory)
    {
        _scope = factory.Services.CreateScope();
    }
}