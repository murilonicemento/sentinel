namespace AlertOrchestrator.Api.Security;

public static class AlertOrchestratorPolicies
{
    public const string Read = "AlertOrchestratorRead";
    public const string Write = "AlertOrchestratorWrite";
}

public static class AlertOrchestratorRoles
{
    public const string Admin = "AlertOrchestrator.Admin";
    public const string Read = "AlertOrchestrator.Read";
    public const string Write = "AlertOrchestrator.Write";
}

public static class SentinelClaims
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string Issuer = "iss";

    /// <summary>Multi-valued claim with the tenants the identity belongs to.</summary>
    public const string TenantId = "tenant_id";
}
