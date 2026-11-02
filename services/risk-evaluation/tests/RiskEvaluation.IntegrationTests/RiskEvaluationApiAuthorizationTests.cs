using System.Net;
using System.Net.Http.Json;
using RiskEvaluation.IntegrationTests.Support;

namespace RiskEvaluation.IntegrationTests;

public class RiskEvaluationApiAuthorizationTests : IClassFixture<RiskEvaluationApiFactory>
{
    private static readonly object EvaluateRequest = new
    {
        latitude = -23,
        longitude = -46,
        timestamp = DateTime.UtcNow,
        metrics = new { windGust = 10 }
    };

    private readonly RiskEvaluationApiFactory _factory;

    public RiskEvaluationApiAuthorizationTests(RiskEvaluationApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Evaluate_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClientWithToken(null)
            .PostAsJsonAsync("/api/risk-evaluation/evaluate", EvaluateRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClientWithToken(null).GetAsync($"/api/risk-evaluation/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_WithReadOnlyRole_Returns403()
    {
        var response = await _factory.CreateClientWithToken(TestTokens.User(["RiskEvaluation.Read"]))
            .PostAsJsonAsync("/api/risk-evaluation/evaluate", EvaluateRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateModel_WithWriteRoleButNotAdmin_Returns403()
    {
        var response = await _factory.CreateClientWithToken(TestTokens.User(["RiskEvaluation.Write"]))
            .PutAsJsonAsync("/api/risk-evaluation/model", new { version = "2" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
