using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace RiskEvaluation.IntegrationTests.Support;

public sealed class RiskEvaluationApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", TestTokens.Issuer);
        builder.UseSetting("Jwt:Audience", TestTokens.Audience);
        builder.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
        builder.UseSetting("ServiceToken:Issuer", "sentinel-service:risk-evaluation");
        builder.UseSetting("ServiceToken:Audience", TestTokens.Audience);
        builder.UseSetting("ServiceToken:SigningKey", "integration-tests-risk-evaluation-service-key-0123456789");
        builder.UseSetting("ServiceToken:Roles:0", "Geospatial.Read");

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
