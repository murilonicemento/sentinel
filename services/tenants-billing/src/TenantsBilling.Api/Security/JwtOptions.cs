using System.ComponentModel.DataAnnotations;

namespace TenantsBilling.Api.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; set; } = string.Empty;

    [Required] public string Audience { get; set; } = string.Empty;

    /// <summary>HS256 key for user tokens. Must come from User Secrets, environment variables or a vault.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Services allowed to call this API with their own signed service tokens (see ADR-0002).</summary>
    public List<TrustedServiceOptions> TrustedServices { get; set; } = [];
}

public sealed class TrustedServiceOptions
{
    public string Name { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Roles the service may claim; a token claiming any other role is rejected.</summary>
    public List<string> AllowedRoles { get; set; } = [];

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(Issuer) &&
        SigningKey.Length >= 32 &&
        AllowedRoles.Count > 0;
}
