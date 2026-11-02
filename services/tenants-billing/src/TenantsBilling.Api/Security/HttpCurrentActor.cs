using TenantsBilling.Application.Auditing;

namespace TenantsBilling.Api.Security;

/// <summary>
/// The actor of an audited action: the <c>sub</c> of the validated token, as <c>Service</c> for a trusted service's
/// token and <c>User</c> otherwise. Outside a request (consumers, workers) the actor is the service itself.
/// </summary>
public sealed class HttpCurrentActor : ICurrentActor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TenantAccessEvaluator _tenantAccess;

    public HttpCurrentActor(IHttpContextAccessor httpContextAccessor, TenantAccessEvaluator tenantAccess)
    {
        _httpContextAccessor = httpContextAccessor;
        _tenantAccess = tenantAccess;
    }

    public AuditActor Get()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var subject = user?.FindFirst(SentinelClaims.Subject)?.Value;
        if (user?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(subject)) return AuditActor.System;

        return new AuditActor(_tenantAccess.IsServicePrincipal(user) ? "Service" : "User", subject);
    }
}
