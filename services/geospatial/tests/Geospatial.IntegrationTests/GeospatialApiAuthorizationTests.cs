using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Geospatial.IntegrationTests.Support;

namespace Geospatial.IntegrationTests;

[Collection(GeospatialApiCollection.Name)]
public class GeospatialApiAuthorizationTests
{
    private const string DistancePath = "/api/geospatial/distance";

    private static readonly object DistanceRequest = new
    {
        from = new { latitude = -23.5505, longitude = -46.6333 },
        to = new { latitude = -22.9068, longitude = -43.1729 }
    };

    private readonly GeospatialApiFactory _factory;

    public GeospatialApiAuthorizationTests(GeospatialApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWith(string? token)
    {
        var client = _factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Distance_WithoutToken_Returns401()
    {
        var response = await ClientWith(null).PostAsJsonAsync(DistancePath, DistanceRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Distance_WithLegacyApiKeyOnly_Returns401()
    {
        var client = ClientWith(null);
        client.DefaultRequestHeaders.Add("X-Api-Key", "CHANGE_ME_REPLACE_WITH_SECURE_KEY");

        var response = await client.PostAsJsonAsync(DistancePath, DistanceRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Distance_WithoutReadRole_Returns403()
    {
        var response = await ClientWith(TestTokens.User(["Other.Role"])).PostAsJsonAsync(DistancePath, DistanceRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Distance_WithReadRole_Returns200()
    {
        var response = await ClientWith(TestTokens.User(["Geospatial.Read"])).PostAsJsonAsync(DistancePath, DistanceRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Distance_WithTrustedServiceToken_Returns200()
    {
        var response = await ClientWith(TestTokens.Service(["Geospatial.Read"])).PostAsJsonAsync(DistancePath, DistanceRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Distance_WithServiceTokenClaimingOnlyNotGrantedRole_Returns403()
    {
        var response = await ClientWith(TestTokens.Service(["Geospatial.Admin"]))
            .PostAsJsonAsync(DistancePath, DistanceRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Distance_WithServiceTokenCarryingRolesOfOtherServices_Returns200()
    {
        var response = await ClientWith(TestTokens.Service(["Geospatial.Read", "TenantsBilling.Read"]))
            .PostAsJsonAsync(DistancePath, DistanceRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
