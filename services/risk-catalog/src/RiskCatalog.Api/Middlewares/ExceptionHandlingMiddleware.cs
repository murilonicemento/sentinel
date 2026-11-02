namespace RiskCatalog.Api.Middlewares;

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
        try
        {
            await _next(httpContext);
        }
        catch (Exception exception) when (
            exception is KeyNotFoundException or ArgumentException or BadHttpRequestException
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
                type = exception.GetType().Name,
                statusCode = StatusCodes.Status500InternalServerError,
                success = false,
                error = exception.Message
            });
        }
    }
}