using System.Text;
using AuditCompliance.Domain.Exceptions;

namespace AuditCompliance.Domain.ValueObjects;

/// <summary>
/// Conteúdo JSON já sanitizado (Metadata, BeforeState ou AfterState). O domínio trata o conteúdo
/// como opaco e garante apenas o limite de tamanho; a minimização é responsabilidade da aplicação.
/// </summary>
public sealed record AuditPayload
{
    public const int MaxSizeInBytes = 16 * 1024;

    private AuditPayload(string json)
    {
        Json = json;
    }

    public string Json { get; }

    public static AuditPayload FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new AuditDomainException(AuditErrorCodes.InvalidField, "Payload must not be empty.");

        if (Encoding.UTF8.GetByteCount(json) > MaxSizeInBytes)
            throw new AuditDomainException(AuditErrorCodes.PayloadLimitExceeded,
                $"Payload exceeds the maximum size of {MaxSizeInBytes} bytes.");

        return new AuditPayload(json);
    }
}
