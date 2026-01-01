namespace RiskCatalog.Api.Middlewares;

public class TenantValidationMiddleware
{
    private readonly RequestDelegate _next;

    public TenantValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized");

            return;
        }

        var tenantClaim = context.User.FindFirst("tenantId");
        if (tenantClaim == null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Tenant claim missing");

            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("X-Tenant-Id header missing");

            return;
        }

        if (!Guid.TryParse(tenantHeader, out var tenantFromHeader) ||
            tenantFromHeader.ToString() != tenantClaim.Value)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Tenant mismatch");

            return;
        }

        await _next(context);
    }
}