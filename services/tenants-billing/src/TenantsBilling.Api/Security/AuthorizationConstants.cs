namespace TenantsBilling.Api.Security;

public static class TenantsBillingPolicies
{
    public const string Read = "TenantsBillingRead";
    public const string Write = "TenantsBillingWrite";
}

public static class TenantsBillingRoles
{
    public const string Admin = "TenantsBilling.Admin";
    public const string Read = "TenantsBilling.Read";
    public const string Write = "TenantsBilling.Write";
}

public static class SentinelClaims
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string Issuer = "iss";

    /// <summary>Multi-valued claim with the tenants the identity belongs to.</summary>
    public const string TenantId = "tenant_id";
}
