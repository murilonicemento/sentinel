using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TenantsBilling.Domain.Events;

namespace TenantsBilling.IntegrationTests.Support;

public sealed class TenantsBillingApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:TenantsBillingDatabase", string.Empty);
        builder.UseSetting("Jwt:Issuer", TestTokens.Issuer);
        builder.UseSetting("Jwt:Audience", TestTokens.Audience);
        builder.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
        builder.UseSetting("Jwt:TrustedServices:0:Name", TestTokens.ServiceName);
        builder.UseSetting("Jwt:TrustedServices:0:Issuer", TestTokens.ServiceIssuer);
        builder.UseSetting("Jwt:TrustedServices:0:SigningKey", TestTokens.ServiceSigningKey);
        builder.UseSetting("Jwt:TrustedServices:0:AllowedRoles:0", "TenantsBilling.Read");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<ITenantEventPublisher>();
            services.AddSingleton<ITenantEventPublisher, NoOpTenantEventPublisher>();
        });
    }

    private sealed class NoOpTenantEventPublisher : ITenantEventPublisher
    {
        public Task PublishAsync(string eventType, object payload, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
