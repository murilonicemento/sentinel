using Ingestion.Api;
using System.Net.Http.Headers;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Ingestion.IntegrationTests.V1;

/// <summary>Hosts the API with a mocked mediator, so the HTTP contract is tested without containers.</summary>
public sealed class IngestionV1ApiFactory : WebApplicationFactory<Program>
{
    public Mock<IMediator> Mediator { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", TestTokens.Issuer);
        builder.UseSetting("Jwt:Audience", TestTokens.Audience);
        builder.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
        builder.UseSetting("ServiceToken:SigningKey", "integration-tests-ingestion-service-token-key-0123456789");
        // The MinIO client is built while services are registered; it is never called because the mediator is mocked.
        builder.UseSetting("MinIO:AccessKey", "integration-tests-access-key");
        builder.UseSetting("MinIO:SecretKey", "integration-tests-secret-key");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IMediator>();
            services.AddSingleton(Mediator.Object);
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
