namespace Geospatial.Api.Security;

public static class GeospatialPolicies
{
    public const string Read = "GeospatialRead";
    public const string Write = "GeospatialWrite";
}

public static class GeospatialRoles
{
    public const string Admin = "Geospatial.Admin";
    public const string Read = "Geospatial.Read";
    public const string Write = "Geospatial.Write";
}

public static class SentinelClaims
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string Issuer = "iss";

    /// <summary>Multi-valued claim with the tenants the identity belongs to.</summary>
    public const string TenantId = "tenant_id";
}
