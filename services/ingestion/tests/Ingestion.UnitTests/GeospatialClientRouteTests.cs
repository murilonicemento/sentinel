using System.Net;
using System.Text;
using Ingestion.Application.DTO;
using Ingestion.Infrastructure.Write.HttpClients;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ingestion.UnitTests;

/// <summary>X-25: the client calls geospatial's v1 routes and reads both the v1 envelope and the frozen bare body.</summary>
public class GeospatialClientRouteTests
{
    private static readonly PointDTO Point = new() { Latitude = -22.9, Longitude = -43.2 };

    private static readonly PolygonDTO Brazil = new()
    {
        Coordinates =
        [
            new PointDTO { Latitude = 5, Longitude = -74 }, new PointDTO { Latitude = 5, Longitude = -34 },
            new PointDTO { Latitude = -34, Longitude = -34 }, new PointDTO { Latitude = -34, Longitude = -74 }
        ]
    };

    private static (GeospatialClient Client, RecordingHandler Handler) Client(string body)
    {
        var handler = new RecordingHandler(body);
        return (new GeospatialClient(new HttpClient(handler) { BaseAddress = new Uri("http://geospatial:8080/") },
            NullLogger<GeospatialClient>.Instance), handler);
    }

    [Fact]
    public async Task ContainsPoint_CallsTheV1OperationAndReadsTheEnvelope()
    {
        var (client, handler) = Client("{\"statusCode\":200,\"success\":true,\"data\":{\"contains\":true}}");

        Assert.True(await client.ContainsPointAsync(Point, Brazil));
        Assert.Equal("/api/v1/geospatial/operations/contains-point", handler.RequestedPath);
        Assert.Contains("\"coordinates\"", handler.RequestBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WithinRadius_ReadsTheV1DistanceInMeters()
    {
        var (client, handler) = Client(
            "{\"statusCode\":200,\"success\":true,\"data\":{\"withinRadius\":true,\"distanceMeters\":1234.5}}");

        var (within, distance) = await client.WithinRadiusAsync(Point, Point, new RadiusDTO { Value = 2, Unit = "km" });

        Assert.True(within);
        Assert.Equal(1234.5, distance);
        Assert.Equal("/api/v1/geospatial/operations/within-radius", handler.RequestedPath);
    }

    [Fact]
    public async Task Intersects_CallsTheV1Operation()
    {
        var (client, handler) = Client("{\"statusCode\":200,\"success\":true,\"data\":{\"intersects\":false}}");

        Assert.False(await client.IntersectsAsync(Brazil, Brazil));
        Assert.Equal("/api/v1/geospatial/operations/intersects", handler.RequestedPath);
    }

    [Fact]
    public async Task Distance_ReadsTheV1DistanceInMeters()
    {
        var (client, handler) = Client("{\"statusCode\":200,\"success\":true,\"data\":{\"distanceMeters\":42.0}}");

        Assert.Equal(42.0, await client.DistanceAsync(Point, Point));
        Assert.Equal("/api/v1/geospatial/operations/distance", handler.RequestedPath);
    }

    [Fact]
    public async Task EvaluateBatch_ReadsTheResultsInsideTheEnvelope()
    {
        var (client, handler) = Client(
            "{\"statusCode\":200,\"success\":true,\"data\":{\"results\":[{\"type\":\"contains-point\",\"contains\":true}]}}");

        var result = await client.EvaluateBatchAsync(new BatchRequestDTO());

        Assert.True(Assert.Single(result.Results).Contains);
        Assert.Equal("/api/v1/geospatial/operations/evaluate-batch", handler.RequestedPath);
    }

    [Fact]
    public async Task BareBodyOfTheFrozenRoutes_IsStillRead()
    {
        var (contains, _) = Client("{\"contains\":true}");
        var (within, _) = Client("{\"withinRadius\":true,\"distance\":10.0}");

        Assert.True(await contains.ContainsPointAsync(Point, Brazil));
        Assert.Equal((true, 10.0), await within.WithinRadiusAsync(Point, Point, new RadiusDTO { Value = 1, Unit = "km" }));
    }

    [Fact]
    public async Task ErrorStatus_Throws()
    {
        var handler = new RecordingHandler("{}", HttpStatusCode.ServiceUnavailable);
        var client = new GeospatialClient(new HttpClient(handler) { BaseAddress = new Uri("http://geospatial:8080/") },
            NullLogger<GeospatialClient>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ContainsPointAsync(Point, Brazil));
    }

    private sealed class RecordingHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? RequestedPath { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestedPath = request.RequestUri?.AbsolutePath;
            RequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
