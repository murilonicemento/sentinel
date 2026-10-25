using System.Security.Claims;

namespace RiskCatalog.Api.Security;

public static class PublisherTenantResolver
{
    // Tokens issued before ADR-0002 carry the tenant in "tenantId"; it is still accepted as a fallback.
    private const string LegacyTenantIdClaim = "tenantId";

    public static bool TryGetTenantId(ClaimsPrincipal user, out Guid tenantId)
    {
        foreach (var claim in user.FindAll(SentinelClaims.TenantId).Concat(user.FindAll(LegacyTenantIdClaim)))
        {
            if (Guid.TryParse(claim.Value, out tenantId) && tenantId != Guid.Empty)
                return true;
        }

        tenantId = Guid.Empty;
        return false;
    }
}
