using System.ComponentModel.DataAnnotations;

namespace AuditCompliance.Api.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; set; } = string.Empty;

    [Required] public string Audience { get; set; } = string.Empty;

    /// <summary>Chave simétrica (HS256). Deve ser fornecida por User Secrets, variável de ambiente ou cofre.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;
}
