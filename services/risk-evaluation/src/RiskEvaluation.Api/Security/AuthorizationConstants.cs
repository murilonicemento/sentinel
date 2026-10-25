namespace RiskEvaluation.Api.Security;

public static class RiskEvaluationPolicies
{
    public const string Read = "RiskEvaluationRead";
    public const string Write = "RiskEvaluationWrite";
}

public static class RiskEvaluationRoles
{
    public const string Admin = "RiskEvaluation.Admin";
    public const string Read = "RiskEvaluation.Read";
    public const string Write = "RiskEvaluation.Write";
}

public static class SentinelClaims
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string Issuer = "iss";

    /// <summary>Multi-valued claim with the tenants the identity belongs to.</summary>
    public const string TenantId = "tenant_id";
}
