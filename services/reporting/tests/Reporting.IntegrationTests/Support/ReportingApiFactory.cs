using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reporting.IntegrationTests.Support;

public sealed class ReportingApiFactory : WebApplicationFactory<Reporting.Api.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:Issuer", TestTokens.Issuer);
        builder.UseSetting("Jwt:Audience", TestTokens.Audience);
        builder.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
    }

    public HttpClient CreateClientWithToken(string? token)
    {
        var client = CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateReaderClient(params string[] tenants) =>
        CreateClientWithToken(TestTokens.User(["Reporting.Read"], tenants));

    public HttpClient CreateWriterClient(params string[] tenants) =>
        CreateClientWithToken(TestTokens.User(["Reporting.Write"], tenants));
}
