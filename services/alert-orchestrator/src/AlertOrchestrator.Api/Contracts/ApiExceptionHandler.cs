using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

namespace AlertOrchestrator.Api.Contracts;

/// <summary>
/// Translates exceptions raised by /api/v1 endpoints into Problem Details. Unexpected failures are logged and
/// returned as a generic 500 so that exception messages and stack traces never reach the client.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        if (!ApiProblems.IsVersionedApi(httpContext)) return false;

        var problem = exception switch
        {
            KeyNotFoundException => ApiProblems.Create(httpContext, StatusCodes.Status404NotFound,
                ApiProblems.ResourceNotFound, "The requested resource was not found."),
            ArgumentException argument => ApiProblems.Create(httpContext, StatusCodes.Status400BadRequest,
                ApiProblems.ValidationError, "One or more fields are invalid.", FieldErrorsFor(argument)),
            BadHttpRequestException => ApiProblems.Create(httpContext, StatusCodes.Status400BadRequest,
                ApiProblems.ValidationError, "The request body is malformed."),
            _ => null
        };

        if (problem is null)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
            problem = ApiProblems.Create(httpContext, StatusCodes.Status500InternalServerError,
                ApiProblems.InternalError, "An unexpected error occurred.");
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        // Serialize with the runtime type so ValidationProblemDetails keeps its field errors.
        await httpContext.Response.WriteAsJsonAsync(problem, problem.GetType(), (JsonSerializerOptions?)null,
            "application/problem+json", cancellationToken);
        return true;
    }

    private static Dictionary<string, string[]> FieldErrorsFor(ArgumentException exception)
    {
        var field = string.IsNullOrWhiteSpace(exception.ParamName) ? "request" : exception.ParamName;
        var message = exception is ArgumentOutOfRangeException
            ? "The value is out of the allowed range."
            : exception.Message.Replace($" (Parameter '{exception.ParamName}')", string.Empty);
        return new Dictionary<string, string[]> { [field] = [message] };
    }
}
