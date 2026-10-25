using Geospatial.Domain.Events;
using Geospatial.Domain.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Geospatial.IntegrationTests.Support;

public sealed class GeospatialApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", TestTokens.Issuer);
        builder.UseSetting("Jwt:Audience", TestTokens.Audience);
        builder.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
        builder.UseSetting("Jwt:TrustedServices:0:Name", TestTokens.ServiceName);
        builder.UseSetting("Jwt:TrustedServices:0:Issuer", TestTokens.ServiceIssuer);
        builder.UseSetting("Jwt:TrustedServices:0:SigningKey", TestTokens.ServiceSigningKey);
        builder.UseSetting("Jwt:TrustedServices:0:AllowedRoles:0", "Geospatial.Read");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IGeospatialEventRepository>();
            services.AddSingleton<IGeospatialEventRepository, InMemoryGeospatialEventRepository>();
        });
    }

    private sealed class InMemoryGeospatialEventRepository : IGeospatialEventRepository
    {
        public Task IndexAsync(GeospatialOperationEvent evt, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<GeospatialOperationEvent>> SearchByPointAsync(double lat, double lon,
            double radiusMeters, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GeospatialOperationEvent>>([]);

        public Task<IReadOnlyList<GeospatialOperationEvent>> SearchByPolygonAsync(
            IReadOnlyList<(double Lat, double Lon)> polygon, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GeospatialOperationEvent>>([]);
    }
}
