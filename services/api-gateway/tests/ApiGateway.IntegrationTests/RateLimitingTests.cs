using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiGateway.IntegrationTests;

public class RateLimitingTests
{
    private const int PermitLimit = 3;

    private static WebApplicationFactory<Program> Gateway() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:PermitLimit", PermitLimit.ToString());
            builder.UseSetting("RateLimiting:WindowSeconds", "60");
        });

    [Fact]
    public async Task RequestsBeyondTheLimit_Get429WithRetryAfterAndProblemDetails()
    {
        await using var factory = Gateway();
        using var client = factory.CreateClient();

        for (var i = 0; i < PermitLimit; i++)
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.GetAsync("/no-such-route")).StatusCode);

        var rejected = await client.GetAsync("/no-such-route");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(rejected.Headers.RetryAfter?.Delta);
        Assert.True(rejected.Headers.RetryAfter!.Delta!.Value.TotalSeconds >= 1);
        using var body = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal(429, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("RATE_LIMIT_EXCEEDED", body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task ForwardedForHeader_DoesNotOpenANewQuota()
    {
        await using var factory = Gateway();
        using var client = factory.CreateClient();

        for (var i = 0; i < PermitLimit; i++)
            await client.GetAsync("/no-such-route");

        using var spoofed = new HttpRequestMessage(HttpMethod.Get, "/no-such-route");
        spoofed.Headers.Add("X-Forwarded-For", "203.0.113.10");

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(spoofed)).StatusCode);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/health")]
    [InlineData("/metrics")]
    public async Task OperationalEndpoints_AreNotLimitedAndAnswerOk(string path)
    {
        await using var factory = Gateway();
        using var client = factory.CreateClient();

        for (var i = 0; i < PermitLimit * 2; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public void InvalidLimit_FailsAtStartup()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:PermitLimit", "0"));

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }
}
