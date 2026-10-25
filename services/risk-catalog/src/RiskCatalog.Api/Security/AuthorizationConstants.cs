namespace RiskCatalog.Api.Security;

public static class RiskCatalogPolicies
{
    public const string Read = "RiskCatalogRead";
    public const string Write = "RiskCatalogWrite";
}

public static class RiskCatalogRoles
{
    public const string Admin = "RiskCatalog.Admin";
    public const string Read = "RiskCatalog.Read";
    public const string Write = "RiskCatalog.Write";
}

public static class SentinelClaims
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string Issuer = "iss";

    /// <summary>Multi-valued claim with the tenants the identity belongs to.</summary>
    public const string TenantId = "tenant_id";
}
