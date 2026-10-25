using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Geospatial.IntegrationTests.Support;

namespace Geospatial.IntegrationTests;

[Collection(GeospatialApiCollection.Name)]
public class GeospatialV1ContractTests
{
    private const string BasePath = "/api/v1/geospatial";

    private readonly GeospatialApiFactory _factory;

    public GeospatialV1ContractTests(GeospatialApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Reader()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.User(["Geospatial.Read"]));
        return client;
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Distance_ReturnsEnvelopeWithDistanceInMeters()
    {
        var response = await Reader().PostAsJsonAsync($"{BasePath}/operations/distance", new
        {
            from = new { latitude = 0, longitude = 0 },
            to = new { latitude = 0, longitude = 1 }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.InRange(data.GetProperty("distanceMeters").GetDouble(), 111_000, 112_000);
    }

    [Fact]
    public async Task Distance_WithInvalidLatitude_ReturnsValidationProblem()
    {
        var response = await Reader().PostAsJsonAsync($"{BasePath}/operations/distance", new
        {
            from = new { latitude = 120, longitude = 0 },
            to = new { latitude = 0, longitude = 1 }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task SearchByRadius_ReturnsPagedEventDtos()
    {
        var response = await Reader().PostAsJsonAsync($"{BasePath}/events/search-by-radius?page=3&pageSize=10",
            new { center = new { latitude = -23.5, longitude = -46.6 }, radiusMeters = 1000 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(25, data.GetProperty("totalItems").GetInt64());
        Assert.Equal(3, data.GetProperty("totalPages").GetInt32());
        Assert.Equal(5, data.GetProperty("items").GetArrayLength());
        Assert.True(data.GetProperty("items")[0].TryGetProperty("collectionId", out _));
    }

    [Fact]
    public async Task SearchByPolygon_WithTooFewPoints_ReturnsFieldError()
    {
        var response = await Reader().PostAsJsonAsync($"{BasePath}/events/search-by-polygon",
            new { polygon = new[] { new { latitude = 0, longitude = 0 }, new { latitude = 1, longitude = 1 } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ProblemAsync(response)).GetProperty("errors").TryGetProperty("polygon", out _));
    }

    [Theory]
    [InlineData("pageSize=51")]
    [InlineData("page=0")]
    public async Task SearchByRadius_WithInvalidPagination_ReturnsValidationProblem(string query)
    {
        var response = await Reader().PostAsJsonAsync($"{BasePath}/events/search-by-radius?{query}",
            new { center = new { latitude = 0, longitude = 0 }, radiusMeters = 1000 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task LegacyRoute_AnnouncesDeprecation()
    {
        var response = await Reader().PostAsJsonAsync("/api/geospatial/distance", new
        {
            from = new { latitude = 0, longitude = 0 },
            to = new { latitude = 0, longitude = 1 }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Deprecation"));
        Assert.Contains("/api/v1/geospatial", response.Headers.GetValues("Link").Single());
    }
}
