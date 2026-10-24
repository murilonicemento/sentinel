using AuditCompliance.Api.Errors;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuditCompliance.Api.Security;

/// <summary>
/// Resolve o tenant da requisição a partir do cabeçalho <c>X-Tenant-Id</c> e o valida contra as
/// claims <c>tenant_id</c> da identidade autenticada. O valor informado pelo cliente nunca é aceito
/// sem essa validação.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TenantContextAttribute : Attribute, IAsyncActionFilter
{
    public const string HeaderName = "X-Tenant-Id";
    private const string ItemKey = "AuditCompliance.TenantId";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;

        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var values) || values.Count != 1 ||
            string.IsNullOrWhiteSpace(values[0]))
        {
            context.Result = ApiProblems.Create(httpContext, StatusCodes.Status400BadRequest, "TENANT_CONTEXT_REQUIRED",
                $"The {HeaderName} header is required.");
            return;
        }

        if (!Guid.TryParse(values[0], out var tenantId) || tenantId == Guid.Empty)
        {
            context.Result = ApiProblems.Create(httpContext, StatusCodes.Status400BadRequest, "INVALID_TENANT_CONTEXT",
                $"The {HeaderName} header must be a valid UUID.");
            return;
        }

        var isMember = httpContext.User.FindAll(AuditClaims.TenantId)
            .Any(claim => Guid.TryParse(claim.Value, out var memberTenant) && memberTenant == tenantId);

        if (!isMember)
        {
            context.Result = ApiProblems.Create(httpContext, StatusCodes.Status403Forbidden, "TENANT_ACCESS_DENIED",
                "The authenticated identity is not authorized for the requested tenant.");
            return;
        }

        httpContext.Items[ItemKey] = tenantId;
        await next();
    }

    public static Guid GetTenantId(HttpContext httpContext)
    {
        return httpContext.Items[ItemKey] as Guid? ??
               throw new InvalidOperationException("Tenant context was not resolved for this request.");
    }
}
