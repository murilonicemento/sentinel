namespace Reporting.Api.Security;

public static class ReportingPolicies
{
    public const string Read = "ReportingRead";
    public const string Write = "ReportingWrite";
}

public static class ReportingRoles
{
    public const string Admin = "Reporting.Admin";
    public const string Read = "Reporting.Read";
    public const string Write = "Reporting.Write";
}

public static class SentinelClaims
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string Issuer = "iss";

    /// <summary>Multi-valued claim with the tenants the identity belongs to.</summary>
    public const string TenantId = "tenant_id";
}
