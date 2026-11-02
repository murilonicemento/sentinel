using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Reporting.Api.Contracts;

/// <summary>RFC 9457 Problem Details with the stable <c>code</c> and <c>traceId</c> extensions.</summary>
public static class ApiProblems
{
    public const string ServiceName = "reporting";
    public const string CodeExtension = "code";
    public const string TraceIdExtension = "traceId";

    public const string ValidationError = "VALIDATION_ERROR";
    public const string ResourceNotFound = "RESOURCE_NOT_FOUND";
    public const string InternalError = "INTERNAL_ERROR";

    public static ProblemDetails Create(HttpContext httpContext, int status, string code, string detail,
        IDictionary<string, string[]>? fieldErrors = null)
    {
        ProblemDetails problem = fieldErrors is { Count: > 0 }
            ? new ValidationProblemDetails(fieldErrors)
            : new ProblemDetails();

        problem.Status = status;
        problem.Title = TitleFor(status);
        problem.Type = TypeFor(code);
        problem.Detail = detail;
        problem.Instance = httpContext.Request.Path;
        Enrich(problem, httpContext, code);
        return problem;
    }

    public static IResult Result(HttpContext httpContext, int status, string code, string detail,
        IDictionary<string, string[]>? fieldErrors = null) =>
        TypedResults.Json(Create(httpContext, status, code, detail, fieldErrors), statusCode: status,
            contentType: "application/problem+json");

    public static IResult Validation(HttpContext httpContext, IDictionary<string, string[]> fieldErrors) =>
        Result(httpContext, StatusCodes.Status400BadRequest, ValidationError,
            "One or more fields are invalid.", fieldErrors);

    public static void Enrich(ProblemDetails problem, HttpContext httpContext, string code)
    {
        problem.Type ??= TypeFor(code);
        problem.Extensions[CodeExtension] = code;
        problem.Extensions[TraceIdExtension] = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
    }

    public static string TypeFor(string code) =>
        $"urn:sentinel:{ServiceName}:error:{code.ToLowerInvariant().Replace('_', '-')}";

    public static string DefaultCodeFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => ValidationError,
        StatusCodes.Status401Unauthorized => "AUTHENTICATION_REQUIRED",
        StatusCodes.Status403Forbidden => "ACCESS_DENIED",
        StatusCodes.Status404NotFound => ResourceNotFound,
        StatusCodes.Status409Conflict => "CONFLICT",
        StatusCodes.Status422UnprocessableEntity => "UNPROCESSABLE_REQUEST",
        StatusCodes.Status429TooManyRequests => "RATE_LIMIT_EXCEEDED",
        StatusCodes.Status503ServiceUnavailable => "SERVICE_UNAVAILABLE",
        _ => InternalError
    };

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Invalid request",
        StatusCodes.Status403Forbidden => "Access denied",
        StatusCodes.Status404NotFound => "Resource not found",
        StatusCodes.Status409Conflict => "Conflict with the current resource state",
        StatusCodes.Status422UnprocessableEntity => "Request cannot be processed",
        _ => "An error occurred"
    };

    public static bool IsVersionedApi(HttpContext httpContext) =>
        httpContext.Request.Path.StartsWithSegments("/api/v1/" + ServiceName);
}
