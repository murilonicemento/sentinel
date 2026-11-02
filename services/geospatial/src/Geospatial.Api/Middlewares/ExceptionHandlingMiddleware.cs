namespace Geospatial.Api.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        // /api/v1 routes report errors as Problem Details through ApiExceptionHandler.
        if (httpContext.Request.Path.StartsWithSegments("/api/v1"))
        {
            await _next(httpContext);
            return;
        }

        try
        {
            await _next(httpContext);
        }
        catch (Exception exception) when (
            exception is KeyNotFoundException or ArgumentException
        )
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                title = "One or more validation errors occurred.",
                type = exception.GetType().Name,
                statusCode = StatusCodes.Status400BadRequest,
                success = false,
                errors = new { messages = new List<string> { exception.Message } }
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception occurred.");
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                title = "An error occurred.",
                type = "InternalServerError",
                statusCode = StatusCodes.Status500InternalServerError,
                success = false,
                error = "An unexpected error occurred."
            });
        }
    }
}