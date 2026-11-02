namespace RiskCatalog.Infrastructure.Security;

/// <summary>
/// Identity this service presents to other Sentinel services (see ADR-0002). The signing key is exclusive to this
/// service and must come from User Secrets, environment variables or a vault.
/// </summary>
public sealed class ServiceTokenOptions
{
    public const string SectionName = "ServiceToken";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];

    public int LifetimeMinutes { get; set; } = 5;
}
