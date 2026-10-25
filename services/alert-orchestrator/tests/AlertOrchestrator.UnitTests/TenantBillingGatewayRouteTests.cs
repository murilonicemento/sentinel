using System.Net;
using System.Text;
using AlertOrchestrator.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlertOrchestrator.UnitTests;

public class TenantBillingGatewayRouteTests
{
    [Fact]
    public async Task ValidateTenantAsync_CallsVersionedRouteAndAcceptsEnvelope()
    {
        var tenantId = Guid.NewGuid().ToString();
        var handler = new RecordingHandler(
            "{\"statusCode\":200,\"success\":true,\"data\":{\"status\":\"Active\"}}");
        var gateway = new TenantBillingGateway(
            new HttpClient(handler) { BaseAddress = new Uri("http://tenants-billing:8080") },
            NullLogger<TenantBillingGateway>.Instance);

        var isActive = await gateway.ValidateTenantAsync(tenantId, CancellationToken.None);

        Assert.True(isActive);
        Assert.Equal($"/api/v1/tenants-billing/tenants/{tenantId}", handler.RequestedPath);
    }

    private sealed class RecordingHandler(string body) : HttpMessageHandler
    {
        public string? RequestedPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestedPath = request.RequestUri?.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
