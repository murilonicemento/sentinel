using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using AuditCompliance.Application.Interfaces;
using AuditCompliance.Domain.Aggregates;

namespace AuditCompliance.Infrastructure.Integrity;

/// <summary>
/// SHA-256 sobre uma representação canônica (ordem de campos fixa, timestamps em milissegundos Unix).
/// O hash detecta alterações acidentais ou não autorizadas por quem não consegue recalculá-lo; não
/// protege contra quem possui privilégio de escrita no banco (ver ADR-0001).
/// </summary>
public sealed class Sha256IntegrityHasher : IIntegrityHasher
{
    private const string Prefix = "sha256:";

    public string Compute(AuditRecord record)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", record.SchemaVersion);
            writer.WriteString("id", record.Id);
            writer.WriteString("tenantId", record.TenantId?.ToString());
            writer.WriteString("eventId", record.EventId);
            writer.WriteString("eventType", record.EventType);
            writer.WriteNumber("eventVersion", record.EventVersion);
            writer.WriteString("action", record.Action);
            writer.WriteString("actorType", record.Actor?.Type.ToString());
            writer.WriteString("actorId", record.Actor?.Id);
            writer.WriteString("sourceService", record.SourceService);
            writer.WriteNumber("occurredAt", record.OccurredAt.ToUnixTimeMilliseconds());
            writer.WriteNumber("recordedAt", record.RecordedAt.ToUnixTimeMilliseconds());
            writer.WriteString("correlationId", record.CorrelationId);
            writer.WriteString("causationId", record.CausationId);
            writer.WriteString("resourceType", record.Resource?.Type);
            writer.WriteString("resourceId", record.Resource?.Id);
            writer.WriteString("metadata", record.Metadata?.Json);
            writer.WriteString("beforeState", record.BeforeState?.Json);
            writer.WriteString("afterState", record.AfterState?.Json);
            writer.WriteEndObject();
        }

        return Prefix + Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
    }

    public bool? Verify(AuditRecord record)
    {
        if (record.IntegrityHash is null) return null;

        return string.Equals(Compute(record), record.IntegrityHash, StringComparison.Ordinal);
    }
}
