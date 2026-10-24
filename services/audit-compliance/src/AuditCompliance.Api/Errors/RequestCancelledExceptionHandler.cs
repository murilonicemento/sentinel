using Microsoft.AspNetCore.Diagnostics;

namespace AuditCompliance.Api.Errors;

/// <summary>
/// Cancelamentos iniciados pelo cliente não são falhas da aplicação e não devem gerar logs de erro.
/// </summary>
public sealed class RequestCancelledExceptionHandler : IExceptionHandler
{
    private const int ClientClosedRequest = 499;

    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not OperationCanceledException || !httpContext.RequestAborted.IsCancellationRequested)
            return ValueTask.FromResult(false);

        httpContext.Response.StatusCode = ClientClosedRequest;
        return ValueTask.FromResult(true);
    }
}
