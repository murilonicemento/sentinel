using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Catalog;
using AuditCompliance.Domain.Enums;
using AuditCompliance.Domain.Repositories;
using AuditCompliance.Domain.ValueObjects;
using AuditCompliance.Infrastructure.Persistence;
using AuditCompliance.Infrastructure.Persistence.Documents;
using AuditCompliance.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace AuditCompliance.IntegrationTests;

public class AuditRecordsApiTests : IClassFixture<AuditComplianceApiFactory>
{
    private const string RecordsPath = "/api/v1/audit-compliance/records";
    private static readonly DateTimeOffset BaseTime = new(2026, 2, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly AuditComplianceApiFactory _factory;

    public AuditRecordsApiTests(AuditComplianceApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Search_WithoutToken_Returns401ProblemDetails()
    {
        var response = await _factory.CreateClient().GetAsync(SearchUrl());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await ReadJsonAsync(response);
        Assert.Equal("AUTHENTICATION_REQUIRED", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Search_WithTokenSignedByUnknownKey_Returns401()
    {
        var tenantId = Guid.NewGuid();
        var token = TestTokens.Create("user-1", ["Audit.Read"], [tenantId],
            "another-signing-key-that-is-not-trusted-000");

        var response = await _factory.CreateClient().Authorize(token, tenantId).GetAsync(SearchUrl());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithoutTenantHeader_Returns400TenantContextRequired()
    {
        var token = TestTokens.Create("user-1", ["Audit.Read"], [Guid.NewGuid()]);

        var response = await _factory.CreateClient().Authorize(token, null).GetAsync(SearchUrl());

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "TENANT_CONTEXT_REQUIRED");
    }

    [Fact]
    public async Task Search_ForTenantOutsideTokenMemberships_Returns403TenantAccessDenied()
    {
        var token = TestTokens.Create("user-1", ["Audit.Read"], [Guid.NewGuid()]);

        var response = await _factory.CreateClient().Authorize(token, Guid.NewGuid()).GetAsync(SearchUrl());

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "TENANT_ACCESS_DENIED");
    }

    [Fact]
    public async Task Search_WithoutReadRole_Returns403()
    {
        var tenantId = Guid.NewGuid();
        var token = TestTokens.Create("user-1", ["Audit.Export"], [tenantId]);

        var response = await _factory.CreateClient().Authorize(token, tenantId).GetAsync(SearchUrl());

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "ACCESS_DENIED");
    }

    [Fact]
    public async Task Search_ReturnsOnlyAuthorizedTenantRecords_PaginatedNewestFirst()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        for (var i = 0; i < 3; i++) await _factory.SeedAsync(NewRecord(tenantA, BaseTime.AddMinutes(i)));
        for (var i = 0; i < 2; i++) await _factory.SeedAsync(NewRecord(tenantB, BaseTime.AddMinutes(i)));

        var client = _factory.CreateClient().Authorize(TestTokens.Create("user-1", ["Audit.Read"], [tenantA]), tenantA);
        var response = await client.GetAsync(SearchUrl() + "&page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("success").GetBoolean());
        var data = body.GetProperty("data");
        Assert.Equal(3, data.GetProperty("totalItems").GetInt64());
        Assert.Equal(2, data.GetProperty("totalPages").GetInt32());

        var items = data.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal(tenantA, item.GetProperty("tenantId").GetGuid()));
        Assert.True(items[0].GetProperty("occurredAt").GetDateTimeOffset() >
                    items[1].GetProperty("occurredAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Search_WithFilters_AppliesThemWithinTenant()
    {
        var tenantId = Guid.NewGuid();
        await _factory.SeedAsync(NewRecord(tenantId, BaseTime, actorId: "alice"));
        await _factory.SeedAsync(NewRecord(tenantId, BaseTime, actorId: "bob"));

        var client = _factory.CreateClient().Authorize(TestTokens.Create("user-1", ["Audit.Read"], [tenantId]), tenantId);
        var response = await client.GetAsync(SearchUrl() + "&actorId=alice&eventType=PlanChanged");

        var items = (await ReadJsonAsync(response)).GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal("alice", items[0].GetProperty("actor").GetProperty("id").GetString());
    }

    [Theory]
    [InlineData("&pageSize=51", "pageSize")]
    [InlineData("&page=0", "page")]
    [InlineData("&sortBy=eventId", "sortBy")]
    public async Task Search_WithInvalidParameters_Returns400ValidationProblem(string query, string field)
    {
        var tenantId = Guid.NewGuid();
        var client = _factory.CreateClient().Authorize(TestTokens.Create("user-1", ["Audit.Read"], [tenantId]), tenantId);

        var response = await client.GetAsync(SearchUrl() + query);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Search_WithoutExplicitInterval_Returns400()
    {
        var tenantId = Guid.NewGuid();
        var client = _factory.CreateClient().Authorize(TestTokens.Create("user-1", ["Audit.Read"], [tenantId]), tenantId);

        var response = await client.GetAsync(RecordsPath + "?from=2026-02-01T00:00:00");

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.True(problem.GetProperty("errors").TryGetProperty("from", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("to", out _));
    }

    [Fact]
    public async Task GetById_ForRecordOfAnotherTenant_Returns404()
    {
        var tenantA = Guid.NewGuid();
        var otherTenantRecord = await _factory.SeedAsync(NewRecord(Guid.NewGuid(), BaseTime));
        var client = _factory.CreateClient().Authorize(TestTokens.Create("user-1", ["Audit.Read"], [tenantA]), tenantA);

        var response = await client.GetAsync($"{RecordsPath}/{otherTenantRecord.Id}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND");
    }

    [Fact]
    public async Task GetById_WithoutSensitiveRole_RedactsStatesAndVerifiesIntegrity()
    {
        var tenantId = Guid.NewGuid();
        var record = await _factory.SeedAsync(NewRecord(tenantId, BaseTime, withStates: true));
        var client = _factory.CreateClient().Authorize(TestTokens.Create("user-1", ["Audit.Read"], [tenantId]), tenantId);

        var data = (await ReadJsonAsync(await client.GetAsync($"{RecordsPath}/{record.Id}"))).GetProperty("data");

        Assert.True(data.GetProperty("statesRedacted").GetBoolean());
        Assert.False(data.TryGetProperty("beforeState", out _));
        Assert.Equal("basic", data.GetProperty("metadata").GetProperty("fromPlan").GetString());
        Assert.True(data.GetProperty("integrityVerified").GetBoolean());
        Assert.StartsWith("sha256:", data.GetProperty("integrityHash").GetString());
    }

    [Fact]
    public async Task GetById_WithSensitiveRole_ReturnsStates()
    {
        var tenantId = Guid.NewGuid();
        var record = await _factory.SeedAsync(NewRecord(tenantId, BaseTime, withStates: true));
        var client = _factory.CreateClient()
            .Authorize(TestTokens.Create("user-1", ["Audit.Read", "Audit.Sensitive"], [tenantId]), tenantId);

        var data = (await ReadJsonAsync(await client.GetAsync($"{RecordsPath}/{record.Id}"))).GetProperty("data");

        Assert.False(data.GetProperty("statesRedacted").GetBoolean());
        Assert.Equal("pro", data.GetProperty("afterState").GetProperty("planId").GetString());
    }

    [Fact]
    public async Task GetById_WhenStoredRecordWasTampered_ReportsIntegrityFailure()
    {
        var tenantId = Guid.NewGuid();
        var record = await _factory.SeedAsync(NewRecord(tenantId, BaseTime, actorId: "alice"));
        var context = _factory.Services.GetRequiredService<AuditComplianceMongoContext>();
        await context.AuditRecords.UpdateOneAsync(d => d.Id == record.Id,
            Builders<AuditRecordDocument>.Update.Set(d => d.Actor!.Id, "mallory"));

        var client = _factory.CreateClient().Authorize(TestTokens.Create("user-1", ["Audit.Read"], [tenantId]), tenantId);
        var data = (await ReadJsonAsync(await client.GetAsync($"{RecordsPath}/{record.Id}"))).GetProperty("data");

        Assert.False(data.GetProperty("integrityVerified").GetBoolean());
    }

    [Fact]
    public async Task Repository_ConcurrentAppendsOfSameEvent_PersistOnlyOnce()
    {
        var tenantId = Guid.NewGuid();
        var eventId = Guid.NewGuid().ToString();
        var repository = _factory.Services.GetRequiredService<IAuditRecordRepository>();

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            repository.AppendAsync(NewRecord(tenantId, BaseTime, eventId: eventId), CancellationToken.None)));

        Assert.Equal(1, results.Count(r => r == AppendResultEnum.Appended));
        Assert.Equal(4, results.Count(r => r == AppendResultEnum.Duplicate));
    }

    [Fact]
    public async Task HealthEndpoints_AreAnonymousAndReportStorageReadiness()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    internal static AuditRecord NewRecord(Guid tenantId, DateTimeOffset occurredAt, string? actorId = "user-1",
        string? eventId = null, bool withStates = false)
    {
        return AuditRecord.Create(
            eventId ?? Guid.NewGuid().ToString(),
            AuditEventCatalog.PlanChanged,
            1,
            "tenants-billing",
            occurredAt,
            occurredAt.AddSeconds(5),
            tenantId,
            "plan.change",
            actorId is null ? null : Actor.Create(ActorTypeEnum.User, actorId),
            "corr-" + Guid.NewGuid().ToString("N")[..8],
            resource: ResourceReference.Create("Tenant", tenantId.ToString()),
            metadata: AuditPayload.FromJson("""{"fromPlan":"basic","toPlan":"pro"}"""),
            beforeState: withStates ? AuditPayload.FromJson("""{"planId":"basic"}""") : null,
            afterState: withStates ? AuditPayload.FromJson("""{"planId":"pro"}""") : null);
    }

    private static string SearchUrl()
    {
        return $"{RecordsPath}?from=2026-02-01T00:00:00Z&to=2026-02-02T00:00:00Z";
    }

    internal static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    internal static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status,
        string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await ReadJsonAsync(response);
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        return problem;
    }
}
