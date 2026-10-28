using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using RiskCatalog.Api;
using RiskCatalog.Application.Services.Interfaces;
using RiskCatalog.Domain.IRepositories;

namespace RiskCatalog.IntegrationTests.Support;

/// <summary>Hosts the API with mocked application services, so the HTTP contract is tested without containers.</summary>
public sealed class RiskCatalogV1ApiFactory : WebApplicationFactory<Program>
{
    public Mock<IEventTypeService> EventTypes { get; } = new();
    public Mock<IRiskModelService> RiskModels { get; } = new();
    public Mock<IRegionalParameterRepository> RegionalParameters { get; } = new();
    public Mock<IRiskMatrixRepository> RiskMatrices { get; } = new();
    public Mock<IEventTypeRepository> EventTypeRepository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", TestTokens.Issuer);
        builder.UseSetting("Jwt:Audience", TestTokens.Audience);
        builder.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
        builder.UseSetting("ServiceToken:SigningKey", "integration-tests-risk-catalog-service-token-key-0123456789");
        builder.UseSetting("ElasticSearch:URI", "http://localhost:9200");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IEventTypeService>();
            services.AddSingleton(EventTypes.Object);
            services.RemoveAll<IRiskModelService>();
            services.AddSingleton(RiskModels.Object);
            services.RemoveAll<IRegionalParameterRepository>();
            services.AddSingleton(RegionalParameters.Object);
            services.RemoveAll<IRiskMatrixRepository>();
            services.AddSingleton(RiskMatrices.Object);
            services.RemoveAll<IEventTypeRepository>();
            services.AddSingleton(EventTypeRepository.Object);
        });
    }

    public HttpClient CreateClientWithToken(string? token)
    {
        var client = CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
