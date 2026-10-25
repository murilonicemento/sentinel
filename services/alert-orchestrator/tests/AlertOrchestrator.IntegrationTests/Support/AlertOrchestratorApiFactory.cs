using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AlertOrchestrator.IntegrationTests.Support;

public sealed class AlertOrchestratorApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:UsePostgreSql", "false");
        builder.UseSetting("Cache:UseRedis", "false");
        builder.UseSetting("Jwt:Issuer", TestTokens.Issuer);
        builder.UseSetting("Jwt:Audience", TestTokens.Audience);
        builder.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
        builder.UseSetting("Jwt:TrustedServices:0:Name", TestTokens.ServiceName);
        builder.UseSetting("Jwt:TrustedServices:0:Issuer", TestTokens.ServiceIssuer);
        builder.UseSetting("Jwt:TrustedServices:0:SigningKey", TestTokens.ServiceSigningKey);
        builder.UseSetting("Jwt:TrustedServices:0:AllowedRoles:0", "AlertOrchestrator.Read");
        builder.UseSetting("ServiceToken:Issuer", "sentinel-service:alert-orchestrator");
        builder.UseSetting("ServiceToken:Audience", TestTokens.Audience);
        builder.UseSetting("ServiceToken:SigningKey", "integration-tests-alert-orchestrator-service-key-0123456789");
        builder.UseSetting("ServiceToken:Roles:0", "TenantsBilling.Read");

        builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
    }

    public HttpClient CreateClientWithToken(string? token)
    {
        var client = CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
