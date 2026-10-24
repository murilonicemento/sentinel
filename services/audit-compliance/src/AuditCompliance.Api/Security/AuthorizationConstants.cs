namespace AuditCompliance.Api.Security;

public static class AuditPolicies
{
    public const string Read = "AuditRead";
    public const string ReadStates = "AuditReadStates";
    public const string Export = "AuditExport";
}

public static class AuditRoles
{
    public const string Admin = "Audit.Admin";
    public const string Read = "Audit.Read";
    public const string ReadSensitive = "Audit.Sensitive";
    public const string Export = "Audit.Export";
}

public static class AuditClaims
{
    public const string Subject = "sub";
    public const string Role = "role";

    /// <summary>Claim multivalorada com os tenants aos quais a identidade pertence.</summary>
    public const string TenantId = "tenant_id";
}

public static class RateLimitPolicies
{
    public const string Queries = "audit-queries";
    public const string Exports = "audit-exports";
}
