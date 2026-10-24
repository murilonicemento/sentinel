using System.Net;
using System.Net.Http.Json;
using AuditCompliance.Application.Exports;
using AuditCompliance.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;
using static AuditCompliance.IntegrationTests.AuditRecordsApiTests;

namespace AuditCompliance.IntegrationTests;

public class AuditExportsApiTests : IClassFixture<AuditComplianceApiFactory>
{
    private const string ExportsPath = "/api/v1/audit-compliance/exports";
    private static readonly DateTimeOffset BaseTime = new(2026, 4, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly AuditComplianceApiFactory _factory;

    public AuditExportsApiTests(AuditComplianceApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExportFlow_GeneratesTenantScopedCsvAndAuditsRequestAndDownload()
    {
        var tenantId = Guid.NewGuid();
        await _factory.SeedAsync(NewRecord(tenantId, BaseTime));
        await _factory.SeedAsync(NewRecord(tenantId, BaseTime.AddMinutes(1)));
        var foreignRecord = await _factory.SeedAsync(NewRecord(Guid.NewGuid(), BaseTime));
        var client = ExportClient(tenantId);

        var requestResponse = await client.PostAsJsonAsync(ExportsPath, new
        {
            format = "csv",
            from = "2026-04-01T00:00:00Z",
            to = "2026-04-02T00:00:00Z"
        });

        Assert.Equal(HttpStatusCode.Accepted, requestResponse.StatusCode);
        Assert.NotNull(requestResponse.Headers.Location);
        var exportId = (await ReadJsonAsync(requestResponse)).GetProperty("data").GetProperty("id").GetGuid();

        var notReady = await client.GetAsync($"{ExportsPath}/{exportId}/download");
        await AssertProblemAsync(notReady, HttpStatusCode.Conflict, "EXPORT_NOT_READY");

        await ProcessPendingExportsAsync();

        var status = (await ReadJsonAsync(await client.GetAsync($"{ExportsPath}/{exportId}"))).GetProperty("data");
        Assert.Equal("Completed", status.GetProperty("status").GetString());
        Assert.Equal(2, status.GetProperty("recordCount").GetInt64());

        var download = await client.GetAsync($"{ExportsPath}/{exportId}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("text/csv", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", download.Headers.CacheControl?.ToString());

        var lines = (await download.Content.ReadAsStringAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("id,tenantId,eventId", lines[0]);
        Assert.DoesNotContain(lines, line => line.Contains(foreignRecord.Id.ToString()));

        var auditClient = _factory.CreateClient()
            .Authorize(TestTokens.Create("auditor", ["Audit.Read"], [tenantId]), tenantId);
        var trail = await ReadJsonAsync(await auditClient.GetAsync(
            $"/api/v1/audit-compliance/records?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(-1).ToString("O"))}" +
            $"&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(1).ToString("O"))}&sourceService=audit-compliance"));

        var eventTypes = trail.GetProperty("data").GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("eventType").GetString()).ToList();
        Assert.Contains("AuditExportRequested", eventTypes);
        Assert.Contains("AuditExportDownloaded", eventTypes);
    }

    [Fact]
    public async Task ExportFlow_JsonFormat_ProducesArrayWithoutStates()
    {
        var tenantId = Guid.NewGuid();
        await _factory.SeedAsync(NewRecord(tenantId, BaseTime, withStates: true));
        var client = ExportClient(tenantId);

        var response = await client.PostAsJsonAsync(ExportsPath,
            new { format = "json", from = "2026-04-01T00:00:00Z", to = "2026-04-02T00:00:00Z" });
        var exportId = (await ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetGuid();
        await ProcessPendingExportsAsync();

        var download = await client.GetAsync($"{ExportsPath}/{exportId}/download");
        var items = (await ReadJsonAsync(download)).EnumerateArray().ToList();

        Assert.Single(items);
        Assert.Equal("pro", items[0].GetProperty("metadata").GetProperty("toPlan").GetString());
        Assert.False(items[0].TryGetProperty("afterState", out _));
    }

    [Fact]
    public async Task Export_OfAnotherTenant_Returns404()
    {
        var owner = Guid.NewGuid();
        var response = await ExportClient(owner).PostAsJsonAsync(ExportsPath,
            new { format = "csv", from = "2026-04-01T00:00:00Z", to = "2026-04-02T00:00:00Z" });
        var exportId = (await ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetGuid();

        var intruder = Guid.NewGuid();
        var intruderResponse = await ExportClient(intruder).GetAsync($"{ExportsPath}/{exportId}/download");

        await AssertProblemAsync(intruderResponse, HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND");
    }

    [Fact]
    public async Task Export_WhenFilterMatchesMoreThanLimit_Returns422()
    {
        var tenantId = Guid.NewGuid();
        for (var i = 0; i < 51; i++) await _factory.SeedAsync(NewRecord(tenantId, BaseTime.AddSeconds(i)));

        var response = await ExportClient(tenantId).PostAsJsonAsync(ExportsPath,
            new { format = "csv", from = "2026-04-01T00:00:00Z", to = "2026-04-02T00:00:00Z" });

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "EXPORT_LIMIT_EXCEEDED");
    }

    [Fact]
    public async Task Export_WithInvalidBody_Returns400()
    {
        var response = await ExportClient(Guid.NewGuid()).PostAsJsonAsync(ExportsPath,
            new { format = "pdf", from = "2026-04-01T00:00:00Z" });

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.True(problem.GetProperty("errors").TryGetProperty("format", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("to", out _));
    }

    [Fact]
    public async Task Export_WithReadOnlyRole_Returns403()
    {
        var tenantId = Guid.NewGuid();
        var client = _factory.CreateClient()
            .Authorize(TestTokens.Create("user-1", ["Audit.Read"], [tenantId]), tenantId);

        var response = await client.PostAsJsonAsync(ExportsPath,
            new { format = "csv", from = "2026-04-01T00:00:00Z", to = "2026-04-02T00:00:00Z" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient ExportClient(Guid tenantId)
    {
        return _factory.CreateClient()
            .Authorize(TestTokens.Create($"user-{tenantId:N}", ["Audit.Export"], [tenantId]), tenantId);
    }

    private async Task ProcessPendingExportsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ProcessAuditExportsHandler>();
        while (await handler.ProcessNextAsync(CancellationToken.None))
        {
        }
    }
}
