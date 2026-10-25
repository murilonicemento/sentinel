using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace ChannelsService.Api.Security;

/// <summary>
/// Decides whether an authenticated principal may act on a tenant. User tokens must carry the tenant in their
/// <c>tenant_id</c> claims; tokens issued by a trusted service act at platform level and carry no tenant.
/// </summary>
public sealed class TenantAccessEvaluator
{
    private readonly string _userIssuer;

    public TenantAccessEvaluator(IOptions<JwtOptions> jwtOptions)
    {
        _userIssuer = jwtOptions.Value.Issuer;
    }

    public bool IsServicePrincipal(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true &&
        user.FindFirst(SentinelClaims.Issuer)?.Value is { } issuer &&
        issuer != _userIssuer;

    public bool CanAccessTenant(ClaimsPrincipal user, Guid tenantId) =>
        IsServicePrincipal(user) ||
        user.FindAll(SentinelClaims.TenantId).Any(claim => Guid.TryParse(claim.Value, out var id) && id == tenantId);

    public bool CanAccessTenant(ClaimsPrincipal user, string? tenantId) =>
        !string.IsNullOrWhiteSpace(tenantId) &&
        (IsServicePrincipal(user) ||
         user.FindAll(SentinelClaims.TenantId).Any(claim =>
             string.Equals(claim.Value, tenantId, StringComparison.OrdinalIgnoreCase)));

    public IReadOnlyList<string> TenantIds(ClaimsPrincipal user) =>
        user.FindAll(SentinelClaims.TenantId).Select(claim => claim.Value).ToList();
}
