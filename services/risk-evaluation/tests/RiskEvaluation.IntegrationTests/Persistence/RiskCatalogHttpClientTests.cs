using System.Net;
using System.Text;
using RiskEvaluation.Domain.Enums;
using RiskEvaluation.Infrastructure.HttpClients;

namespace RiskEvaluation.IntegrationTests.Persistence;

/// <summary>The client against the JSON shape of risk-catalog's v1 listings (envelope, camelCase, pagination).</summary>
public class RiskCatalogHttpClientTests
{
    private static string Page(string items, int totalPages) =>
        $"{{\"statusCode\":200,\"success\":true,\"data\":{{\"items\":[{items}],\"page\":1,\"pageSize\":50,\"totalItems\":1,\"totalPages\":{totalPages}}}}}";

    [Fact]
    public async Task FetchSnapshot_WalksEveryPageOfTheThreeListings()
    {
        var handler = new StubHandler(request => request.RequestUri!.PathAndQuery switch
        {
            var p when p.StartsWith("/api/v1/risk-catalog/regional-risk-parameters") && p.Contains("page=1&") =>
                Page("{\"regionId\":\"11111111-1111-1111-1111-111111111111\",\"adjustmentFactor\":1.4,\"description\":\"a\",\"centerLatitude\":-23.5,\"centerLongitude\":-46.6,\"regionBounds\":null,\"coverageRadiusKm\":20}", 2),
            var p when p.StartsWith("/api/v1/risk-catalog/regional-risk-parameters") =>
                Page("{\"regionId\":\"22222222-2222-2222-2222-222222222222\",\"adjustmentFactor\":0.8,\"description\":\"b\",\"regionBounds\":{\"coordinates\":[{\"latitude\":-1,\"longitude\":-1},{\"latitude\":-1,\"longitude\":1},{\"latitude\":1,\"longitude\":1}]}}", 2),
            var p when p.StartsWith("/api/v1/risk-catalog/severity-criteria?version=3") =>
                Page("{\"eventTypeCode\":\"Flood\",\"severityLevel\":\"High\",\"minValue\":5,\"maxValue\":10,\"unit\":\"m\",\"version\":3}", 1),
            var p when p.StartsWith("/api/v1/risk-catalog/risk-matrix-entries?version=3") =>
                Page("{\"eventTypeCode\":\"Flood\",\"severityLevel\":\"High\",\"riskLevel\":\"Critical\",\"version\":3}", 1),
            var p => throw new InvalidOperationException($"Unexpected request {p}")
        });
        var client = new RiskCatalogHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://risk-catalog:8080") });

        var snapshot = await client.FetchSnapshotAsync(3);

        Assert.Equal(3, snapshot.Version);
        Assert.Equal(2, snapshot.Regions.Count);
        Assert.Equal(3, snapshot.Regions[1].Polygon.Count);
        Assert.Equal(1.4, snapshot.RegionalFactorAt(-23.5, -46.6));
        Assert.Equal(RiskLevel.Critical, snapshot.MatrixLevelFor("Flood", 7));
    }

    [Fact]
    public async Task FetchSnapshot_WhenRiskCatalogFails_ThrowsWithTheStatus()
    {
        var client = new RiskCatalogHttpClient(new HttpClient(new StubHandler(_ => null))
        { BaseAddress = new Uri("http://risk-catalog:8080") });

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchSnapshotAsync(3));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, string?> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = respond(request);
            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
