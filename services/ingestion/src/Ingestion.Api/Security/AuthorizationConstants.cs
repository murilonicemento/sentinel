namespace Ingestion.Api.Security;

public static class IngestionPolicies
{
    public const string Read = "IngestionRead";
    public const string Write = "IngestionWrite";
}

public static class IngestionRoles
{
    public const string Admin = "Ingestion.Admin";
    public const string Read = "Ingestion.Read";
    public const string Write = "Ingestion.Write";
}

public static class SentinelClaims
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string Issuer = "iss";

    /// <summary>Multi-valued claim with the tenants the identity belongs to.</summary>
    public const string TenantId = "tenant_id";
}
