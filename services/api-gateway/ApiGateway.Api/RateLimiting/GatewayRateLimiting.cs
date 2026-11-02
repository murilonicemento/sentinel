using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ApiGateway.Api.RateLimiting;

/// <summary>Fixed window per client IP (section <c>RateLimiting</c>).</summary>
public sealed class GatewayRateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Requests one client IP may send per window.</summary>
    [Range(1, 1_000_000)]
    public int PermitLimit { get; init; } = 300;

    [Range(1, 3600)]
    public int WindowSeconds { get; init; } = 60;
}

/// <summary>
/// Gateway rate limit (ADR-0004). The client is identified by the TCP peer address only: <c>X-Forwarded-For</c> is not
/// trusted, because no trusted proxy sits in front of the gateway. Health checks and <c>/metrics</c> are not limited.
/// Rejections answer 429 with <c>Retry-After</c> and an RFC 9457 body (<c>code</c> <c>RATE_LIMIT_EXCEEDED</c>).
/// </summary>
public static class GatewayRateLimiting
{
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";

    public static IServiceCollection AddGatewayRateLimiting(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<GatewayRateLimitOptions>()
            .Bind(configuration.GetSection(GatewayRateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (IsExempt(context.Request.Path)) return RateLimitPartition.GetNoLimiter("exempt");

                var options = context.RequestServices.GetRequiredService<IOptions<GatewayRateLimitOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(ClientKey(context.Connection.RemoteIpAddress),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.PermitLimit,
                        Window = TimeSpan.FromSeconds(options.WindowSeconds),
                        QueueLimit = 0
                    });
            });
            limiter.OnRejected = WriteRejectionAsync;
        });
        return services;
    }

    private static bool IsExempt(PathString path) =>
        path.StartsWithSegments("/health") || path.StartsWithSegments("/metrics");

    private static string ClientKey(IPAddress? address) =>
        address is null ? "unknown" : (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();

    private static async ValueTask WriteRejectionAsync(OnRejectedContext rejected, CancellationToken cancellationToken)
    {
        var response = rejected.HttpContext.Response;
        if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            response.Headers.RetryAfter =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

        await response.WriteAsJsonAsync(new
        {
            type = "https://httpwg.org/specs/rfc6585.html#status-429",
            title = "Too Many Requests",
            status = StatusCodes.Status429TooManyRequests,
            detail = "The request rate limit was exceeded. Retry after the time in the Retry-After header.",
            instance = rejected.HttpContext.Request.Path.Value,
            code = RateLimitExceeded,
            traceId = Activity.Current?.TraceId.ToString() ?? rejected.HttpContext.TraceIdentifier
        }, options: null, contentType: "application/problem+json", cancellationToken);
    }
}
