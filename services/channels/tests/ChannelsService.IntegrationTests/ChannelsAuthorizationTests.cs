using System.Net;
using System.Net.Http.Json;
using ChannelsService.IntegrationTests.Support;

namespace ChannelsService.IntegrationTests;

/// <summary>Authentication, roles and tenant isolation through the real JWT pipeline, on v1 and legacy routes.</summary>
public class ChannelsAuthorizationTests : IClassFixture<ChannelsApiFactory>
{
    private readonly ChannelsApiFactory _factory;

    public ChannelsAuthorizationTests(ChannelsApiFactory factory)
    {
        _factory = factory;
    }

    private static object Notification(string tenantId, string eventId) => new
    {
        eventId,
        tenantId,
        priority = "High",
        message = new { title = "Flood alert", body = "Leave the area" },
        channels = new[] { "Sms" }
    };

    public static TheoryData<string, string> Routes => new()
    {
        { "POST", "/api/v1/channels/notifications" },
        { "GET", "/api/v1/channels/notifications/evt-1/attempts" },
        { "POST", "/api/v1/channels/dead-letters/reprocess" },
        { "POST", "/api/notifications" },
        { "GET", "/api/notifications/evt-1/attempts" },
        { "POST", "/api/dlq/reprocess" }
    };

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path,
        string tenantId = "tenant-a", string eventId = "evt-1") =>
        method == "GET"
            ? client.GetAsync(path)
            : client.PostAsJsonAsync(path, Notification(tenantId, eventId));

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task EveryRoute_WithoutToken_Returns401(string method, string path)
    {
        var response = await SendAsync(_factory.CreateClientWithToken(null), method, path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task EveryRoute_WithATokenFromAnotherIssuer_Returns401(string method, string path)
    {
        var forged = TestTokens.Create("someone-else", "forged-signing-key-0123456789-0123456789", "user-1",
            ["Channels.Write", "Channels.Read"], ["tenant-a"]);

        var response = await SendAsync(_factory.CreateClientWithToken(forged), method, path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/channels/notifications")]
    [InlineData("/api/notifications")]
    public async Task Send_WithAReadOnlyToken_Returns403AndDispatchesNothing(string path)
    {
        var eventId = $"evt-{Guid.NewGuid():N}";
        var reader = _factory.CreateClientWithToken(TestTokens.User(["Channels.Read"], ["tenant-a"]));

        var response = await reader.PostAsJsonAsync(path, Notification("tenant-a", eventId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(eventId, _factory.Dispatch.Dispatched);
    }

    [Theory]
    [InlineData("/api/v1/channels/notifications")]
    [InlineData("/api/notifications")]
    public async Task Send_ForATenantOutsideTheToken_Returns403AndDispatchesNothing(string path)
    {
        var eventId = $"evt-{Guid.NewGuid():N}";
        var writer = _factory.CreateClientWithToken(TestTokens.User(["Channels.Write"], ["tenant-a"]));

        var response = await writer.PostAsJsonAsync(path, Notification("tenant-b", eventId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(eventId, _factory.Dispatch.Dispatched);
    }

    [Theory]
    [InlineData("/api/v1/channels/dead-letters/reprocess")]
    [InlineData("/api/dlq/reprocess")]
    public async Task Reprocess_ForATenantOutsideTheToken_Returns403AndPublishesNothing(string path)
    {
        var eventId = $"evt-{Guid.NewGuid():N}";
        var writer = _factory.CreateClientWithToken(TestTokens.User(["Channels.Write"], ["tenant-a"]));

        var response = await writer.PostAsJsonAsync(path, Notification("tenant-b", eventId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(_factory.Publisher.Published, published => published.EventId == eventId);
    }
}
