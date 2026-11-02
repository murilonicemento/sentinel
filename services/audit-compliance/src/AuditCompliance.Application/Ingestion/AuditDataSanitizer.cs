using System.Buffers;
using System.Text;
using System.Text.Json;
using AuditCompliance.Domain.Exceptions;

namespace AuditCompliance.Application.Ingestion;

public sealed record SanitizedPayload(string Json, int RedactedFields);

/// <summary>
/// Aplica as regras de minimização sobre Metadata, BeforeState e AfterState: restringe estrutura,
/// profundidade e tamanho, e substitui valores de chaves sensíveis (credenciais e dados pessoais
/// diretos) por um marcador, sem copiá-los para o armazenamento de auditoria.
/// </summary>
public static class AuditDataSanitizer
{
    public const string RedactedValue = "[REDACTED]";
    public const int MaxDepth = 5;
    public const int MaxPropertiesPerObject = 50;
    public const int MaxArrayLength = 50;
    public const int MaxStringLength = 1024;

    private static readonly string[] SensitiveKeyFragments =
    [
        "password", "passwd", "secret", "token", "apikey", "credential", "privatekey", "authorization",
        "connectionstring", "cookie"
    ];

    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.Ordinal)
    {
        "cpf", "rg", "cvv", "cardnumber", "creditcard", "email", "phone", "phonenumber", "mobile"
    };

    public static SanitizedPayload Sanitize(JsonElement element, string fieldName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new AuditDomainException(AuditErrorCodes.InvalidField, $"{fieldName} must be a JSON object.");

        var buffer = new ArrayBufferWriter<byte>();
        var redacted = 0;

        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteObject(writer, element, 1, fieldName, ref redacted);
        }

        return new SanitizedPayload(Encoding.UTF8.GetString(buffer.WrittenSpan), redacted);
    }

    public static bool IsSensitiveKey(string key)
    {
        var normalized = new string(key.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        return SensitiveKeys.Contains(normalized) ||
               SensitiveKeyFragments.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal));
    }

    private static void WriteObject(Utf8JsonWriter writer, JsonElement element, int depth, string fieldName,
        ref int redacted)
    {
        if (depth > MaxDepth)
            throw LimitExceeded(fieldName, $"maximum depth of {MaxDepth}");

        writer.WriteStartObject();
        var count = 0;

        foreach (var property in element.EnumerateObject())
        {
            if (++count > MaxPropertiesPerObject)
                throw LimitExceeded(fieldName, $"maximum of {MaxPropertiesPerObject} properties per object");

            writer.WritePropertyName(property.Name);

            if (IsSensitiveKey(property.Name))
            {
                writer.WriteStringValue(RedactedValue);
                redacted++;
                continue;
            }

            WriteValue(writer, property.Value, depth, fieldName, ref redacted);
        }

        writer.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter writer, JsonElement value, int depth, string fieldName,
        ref int redacted)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                WriteObject(writer, value, depth + 1, fieldName, ref redacted);
                break;
            case JsonValueKind.Array:
                if (depth + 1 > MaxDepth)
                    throw LimitExceeded(fieldName, $"maximum depth of {MaxDepth}");

                writer.WriteStartArray();
                var items = 0;
                foreach (var item in value.EnumerateArray())
                {
                    if (++items > MaxArrayLength)
                        throw LimitExceeded(fieldName, $"maximum of {MaxArrayLength} array items");

                    WriteValue(writer, item, depth + 1, fieldName, ref redacted);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                var text = value.GetString()!;
                if (text.Length > MaxStringLength)
                    throw LimitExceeded(fieldName, $"maximum string length of {MaxStringLength}");

                writer.WriteStringValue(text);
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }

    private static AuditDomainException LimitExceeded(string fieldName, string limit)
    {
        return new AuditDomainException(AuditErrorCodes.PayloadLimitExceeded, $"{fieldName} exceeds the {limit}.");
    }
}
