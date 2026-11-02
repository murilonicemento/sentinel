using System.Globalization;
using Microsoft.AspNetCore.OpenApi;

namespace TenantsBilling.Api.Contracts;

/// <summary>Marks an endpoint replaced by a /api/v1 route (90-day deprecation period, api-design.md).</summary>
public sealed record DeprecatedEndpointMetadata(string SuccessorPath);

public static class Deprecation
{
    public static readonly DateTimeOffset AnnouncedAt = new(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset SunsetAt = AnnouncedAt.AddDays(90);

    public static TBuilder MarkDeprecated<TBuilder>(this TBuilder builder, string successorPath)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new DeprecatedEndpointMetadata(successorPath));
        builder.AddEndpointFilter(async (context, next) =>
        {
            AddHeaders(context.HttpContext.Response.Headers, successorPath);
            return await next(context);
        });
        return builder;
    }

    public static void AddHeaders(IHeaderDictionary headers, string successorPath)
    {
        headers["Deprecation"] = $"@{AnnouncedAt.ToUnixTimeSeconds()}";
        headers["Sunset"] = SunsetAt.ToString("R", CultureInfo.InvariantCulture);
        headers["Link"] = $"<{successorPath}>; rel=\"successor-version\"";
    }

    public static OpenApiOptions MarkDeprecatedOperations(this OpenApiOptions options)
    {
        options.AddOperationTransformer((operation, context, _) =>
        {
            if (context.Description.ActionDescriptor.EndpointMetadata.OfType<DeprecatedEndpointMetadata>().Any())
                operation.Deprecated = true;
            return Task.CompletedTask;
        });
        return options;
    }
}
