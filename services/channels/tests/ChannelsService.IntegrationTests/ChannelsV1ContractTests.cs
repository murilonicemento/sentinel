using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using ChannelsService.IntegrationTests.Support;

namespace ChannelsService.IntegrationTests;

public class ChannelsV1ContractTests : IClassFixture<ChannelsApiFactory>
{
    private const string BasePath = "/api/v1/channels";

    private readonly ChannelsApiFactory _factory;

    public ChannelsV1ContractTests(ChannelsApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Writer(params string[] tenants) =>
        _factory.CreateClientWithToken(TestTokens.User(["Channels.Write"], tenants));

    private static object Notification(string tenantId, string eventId = "evt-1") => new
    {
        eventId,
        tenantId,
        priority = "High",
        message = new { title = "Flood alert", body = "Leave the area" },
        channels = new[] { "Sms" }
    };

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Send_WhenDelivered_ReturnsEnvelope()
    {
        _factory.Dispatch.Succeeds = true;

        var response = await Writer("tenant-a").PostAsJsonAsync($"{BasePath}/notifications", Notification("tenant-a"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("data").GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Send_WhenDeliveryFails_Returns502WithoutProviderDetails()
    {
        _factory.Dispatch.Succeeds = false;
        try
        {
            var response = await Writer("tenant-a").PostAsJsonAsync($"{BasePath}/notifications", Notification("tenant-a"));

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            var problem = await ProblemAsync(response);
            Assert.Equal("NOTIFICATION_DELIVERY_FAILED", problem.GetProperty("code").GetString());
            Assert.DoesNotContain("10.0.0.12", problem.GetRawText());
        }
        finally
        {
            _factory.Dispatch.Succeeds = true;
        }
    }

    [Fact]
    public async Task Send_ForAnotherTenant_ReturnsTenantAccessDenied()
    {
        var response = await Writer("tenant-a").PostAsJsonAsync($"{BasePath}/notifications", Notification("tenant-b"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("TENANT_ACCESS_DENIED", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Send_WithoutChannels_ReturnsFieldError()
    {
        var response = await Writer("tenant-a").PostAsJsonAsync($"{BasePath}/notifications", new
        {
            eventId = "evt-2",
            tenantId = "tenant-a",
            message = new { title = "t", body = "b" },
            channels = Array.Empty<string>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.Equal("VALIDATION_ERROR", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("Channels", out _));
    }

    [Fact]
    public async Task GetAttempts_ReturnsChronologicalPageOfOwnTenantAttempts()
    {
        var eventId = $"evt-{Guid.NewGuid():N}";
        var start = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
        await _factory.Attempts.AddAsync(new DeliveryAttempt
        { AttemptId = "b", EventId = eventId, TenantId = "tenant-a", Channel = ChannelTypeEnum.Sms, Timestamp = start.AddSeconds(2) });
        await _factory.Attempts.AddAsync(new DeliveryAttempt
        { AttemptId = "a", EventId = eventId, TenantId = "tenant-a", Channel = ChannelTypeEnum.Email, Timestamp = start });
        await _factory.Attempts.AddAsync(new DeliveryAttempt
        { AttemptId = "c", EventId = eventId, TenantId = "tenant-b", Channel = ChannelTypeEnum.Sms, Timestamp = start.AddSeconds(1) });

        var body = await _factory.CreateClientWithToken(TestTokens.User(["Channels.Read"], ["tenant-a"]))
            .GetFromJsonAsync<JsonElement>($"{BasePath}/notifications/{eventId}/attempts");

        var data = body.GetProperty("data");
        Assert.Equal(2, data.GetProperty("totalItems").GetInt64());
        Assert.Equal(["a", "b"], data.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("attemptId").GetString()));
        Assert.Equal("Email", data.GetProperty("items")[0].GetProperty("channel").GetString());
    }

    [Fact]
    public async Task Reprocess_PublishesNotificationAndReturnsAccepted()
    {
        var eventId = $"evt-{Guid.NewGuid():N}";

        var response = await Writer("tenant-a").PostAsJsonAsync($"{BasePath}/dead-letters/reprocess",
            Notification("tenant-a", eventId));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Contains(_factory.Publisher.Published, n => n.EventId == eventId);
    }

    [Fact]
    public async Task PrometheusMetrics_AreAnonymous()
    {
        var response = await _factory.CreateClientWithToken(null).GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LegacyRoute_AnnouncesDeprecation()
    {
        var response = await _factory.CreateClientWithToken(TestTokens.User(["Channels.Read"], ["tenant-a"]))
            .GetAsync("/api/notifications/evt-legacy/attempts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Deprecation"));
        Assert.Contains("/api/v1/channels/notifications", response.Headers.GetValues("Link").Single());
    }
}
