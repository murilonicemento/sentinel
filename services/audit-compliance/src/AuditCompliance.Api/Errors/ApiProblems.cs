using System.Diagnostics;
using AuditCompliance.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace AuditCompliance.Api.Errors;

/// <summary>
/// Respostas de erro no formato RFC 9457 (Problem Details) com as extensões estáveis
/// <c>code</c> (código de erro da aplicação) e <c>traceId</c>.
/// </summary>
public static class ApiProblems
{
    public const string CodeExtension = "code";
    public const string TraceIdExtension = "traceId";

    public static ObjectResult FromError(HttpContext httpContext, Error error)
    {
        var status = error.Type switch
        {
            ErrorTypeEnum.Validation => StatusCodes.Status400BadRequest,
            ErrorTypeEnum.NotFound => StatusCodes.Status404NotFound,
            ErrorTypeEnum.Conflict => StatusCodes.Status409Conflict,
            ErrorTypeEnum.Gone => StatusCodes.Status410Gone,
            ErrorTypeEnum.Unprocessable => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError
        };

        return Create(httpContext, status, error.Code, error.Message, error.FieldErrors);
    }

    public static ObjectResult Create(HttpContext httpContext, int status, string code, string detail,
        IReadOnlyDictionary<string, string[]>? fieldErrors = null)
    {
        ProblemDetails problem = fieldErrors is { Count: > 0 }
            ? new ValidationProblemDetails(fieldErrors.ToDictionary(e => e.Key, e => e.Value))
            : new ProblemDetails();

        problem.Status = status;
        problem.Title = TitleFor(status);
        problem.Type = TypeFor(code);
        problem.Detail = detail;
        problem.Instance = httpContext.Request.Path;
        problem.Extensions[CodeExtension] = code;
        problem.Extensions[TraceIdExtension] = TraceId(httpContext);

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }

    public static string TraceId(HttpContext httpContext)
    {
        return Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
    }

    public static string TypeFor(string code)
    {
        return $"urn:sentinel:audit-compliance:error:{code.ToLowerInvariant().Replace('_', '-')}";
    }

    public static string DefaultCodeFor(int status)
    {
        return status switch
        {
            StatusCodes.Status400BadRequest => ApplicationErrorCodes.ValidationError,
            StatusCodes.Status401Unauthorized => "AUTHENTICATION_REQUIRED",
            StatusCodes.Status403Forbidden => "ACCESS_DENIED",
            StatusCodes.Status404NotFound => ApplicationErrorCodes.ResourceNotFound,
            StatusCodes.Status429TooManyRequests => "RATE_LIMIT_EXCEEDED",
            StatusCodes.Status503ServiceUnavailable => "SERVICE_UNAVAILABLE",
            _ => "INTERNAL_ERROR"
        };
    }

    private static string TitleFor(int status)
    {
        return status switch
        {
            StatusCodes.Status400BadRequest => "Invalid request",
            StatusCodes.Status403Forbidden => "Access denied",
            StatusCodes.Status404NotFound => "Resource not found",
            StatusCodes.Status409Conflict => "Conflict with the current resource state",
            StatusCodes.Status410Gone => "Resource no longer available",
            StatusCodes.Status422UnprocessableEntity => "Request cannot be processed",
            _ => "An error occurred"
        };
    }
}
