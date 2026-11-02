using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using TenantsBilling.Application.Auditing;
using TenantsBilling.Domain.Events;

namespace TenantsBilling.IntegrationTests.Support;

public sealed class TenantsBillingApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Audit events the API published, in order.</summary>
    public RecordingAuditPublisher Audit { get; } = new();

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
            services.RemoveAll<IAuditEventPublisher>();
            services.AddSingleton<IAuditEventPublisher>(Audit);
        });
    }

    private sealed class NoOpTenantEventPublisher : ITenantEventPublisher
    {
        public Task PublishAsync(string eventType, object payload, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

public sealed class RecordingAuditPublisher : IAuditEventPublisher
{
    public ConcurrentQueue<AuditEventEnvelope> Published { get; } = new();

    public Task PublishAsync(AuditEventEnvelope auditEvent, CancellationToken cancellationToken = default)
    {
        Published.Enqueue(auditEvent);
        return Task.CompletedTask;
    }
}
