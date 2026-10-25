using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Enums;
using RiskEvaluation.IntegrationTests.Support;

namespace RiskEvaluation.IntegrationTests;

public class RiskEvaluationV1ContractTests : IClassFixture<RiskEvaluationApiFactory>
{
    private const string BasePath = "/api/v1/risk-evaluation";

    private readonly RiskEvaluationApiFactory _factory;

    public RiskEvaluationV1ContractTests(RiskEvaluationApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client(params string[] roles) => _factory.CreateClientWithToken(TestTokens.User(roles));

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Evaluate_AtEquatorAndPrimeMeridian_IsAcceptedAndReturnsCreatedEnvelope()
    {
        var response = await Client("RiskEvaluation.Write").PostAsJsonAsync($"{BasePath}/evaluations", new
        {
            latitude = 0,
            longitude = 0,
            timestamp = DateTime.UtcNow,
            metrics = new { windGust = 50 }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("data").GetProperty("id").GetGuid();
        Assert.Equal($"{BasePath}/evaluations/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal(201, body.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task Evaluate_WithOutOfRangeLatitude_ReturnsFieldError()
    {
        var response = await Client("RiskEvaluation.Write").PostAsJsonAsync($"{BasePath}/evaluations", new
        {
            latitude = 91,
            longitude = 0,
            timestamp = DateTime.UtcNow,
            metrics = new { windGust = 50 }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.Equal("VALIDATION_ERROR", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("Latitude", out _));
    }

    [Fact]
    public async Task History_ReturnsPagedEvaluationsNewestFirst()
    {
        var older = new RiskEvaluationEntity(10, 20, 0.2, RiskLevel.Low);
        await Task.Delay(TimeSpan.FromMilliseconds(5));
        var newer = new RiskEvaluationEntity(10, 20, 0.7, RiskLevel.High);
        await _factory.Evaluations.AddAsync(older);
        await _factory.Evaluations.AddAsync(newer);

        var body = await Client("RiskEvaluation.Read")
            .GetFromJsonAsync<JsonElement>($"{BasePath}/evaluations?latitude=10&longitude=20&pageSize=1");

        var data = body.GetProperty("data");
        Assert.Equal(2, data.GetProperty("totalItems").GetInt64());
        Assert.Equal(2, data.GetProperty("totalPages").GetInt32());
        Assert.Equal(newer.Id, data.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData("longitude=20", "latitude")]
    [InlineData("latitude=10", "longitude")]
    [InlineData("latitude=10&longitude=20&pageSize=100", "pageSize")]
    public async Task History_WithMissingOrInvalidParameter_ReturnsFieldError(string query, string field)
    {
        var response = await Client("RiskEvaluation.Read").GetAsync($"{BasePath}/evaluations?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ProblemAsync(response)).GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsNotFoundProblem()
    {
        var response = await Client("RiskEvaluation.Read").GetAsync($"{BasePath}/evaluations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("RISK_EVALUATION_NOT_FOUND", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task LegacyRoute_AnnouncesDeprecation()
    {
        var response = await Client("RiskEvaluation.Read").GetAsync($"/api/risk-evaluation/{Guid.NewGuid()}");

        Assert.True(response.Headers.Contains("Deprecation"));
        Assert.Contains("/api/v1/risk-evaluation", response.Headers.GetValues("Link").Single());
    }
}
