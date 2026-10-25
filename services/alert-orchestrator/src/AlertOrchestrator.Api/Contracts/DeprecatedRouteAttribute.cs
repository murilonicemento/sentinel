using System.Globalization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.OpenApi;

namespace AlertOrchestrator.Api.Contracts;

/// <summary>
/// Marks a controller or action replaced by a /api/v1 route (90-day deprecation period, api-design.md). Adds the
/// Deprecation, Sunset and Link headers and flags the operation as deprecated in OpenAPI.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DeprecatedRouteAttribute : Attribute, IResultFilter
{
    public static readonly DateTimeOffset AnnouncedAt = new(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset SunsetAt = AnnouncedAt.AddDays(90);

    public DeprecatedRouteAttribute(string successorPath)
    {
        SuccessorPath = successorPath;
    }

    public string SuccessorPath { get; }

    public void OnResultExecuting(ResultExecutingContext context)
    {
        var headers = context.HttpContext.Response.Headers;
        headers["Deprecation"] = $"@{AnnouncedAt.ToUnixTimeSeconds()}";
        headers["Sunset"] = SunsetAt.ToString("R", CultureInfo.InvariantCulture);
        headers["Link"] = $"<{SuccessorPath}>; rel=\"successor-version\"";
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}

public static class DeprecatedRouteOpenApi
{
    public static OpenApiOptions MarkDeprecatedOperations(this OpenApiOptions options)
    {
        options.AddOperationTransformer((operation, context, _) =>
        {
            if (context.Description.ActionDescriptor.EndpointMetadata.OfType<DeprecatedRouteAttribute>().Any())
                operation.Deprecated = true;
            return Task.CompletedTask;
        });
        return options;
    }
}
