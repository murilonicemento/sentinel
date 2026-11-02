using System.Text.Json;
using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Enums;

namespace AuditCompliance.Application.Exports.Formatters;

public sealed class JsonAuditExportFormatter : IAuditExportFormatter
{
    public ExportFormatEnum Format => ExportFormatEnum.Json;
    public string ContentType => "application/json";

    public async Task<long> WriteAsync(IAsyncEnumerable<AuditRecord> records, Stream output,
        CancellationToken cancellationToken)
    {
        await using var writer = new Utf8JsonWriter(output);
        writer.WriteStartArray();

        long count = 0;
        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            writer.WriteStartObject();
            writer.WriteString("id", record.Id);
            WriteOptional(writer, "tenantId", record.TenantId?.ToString());
            writer.WriteString("eventId", record.EventId);
            writer.WriteString("eventType", record.EventType);
            writer.WriteNumber("eventVersion", record.EventVersion);
            WriteOptional(writer, "action", record.Action);

            if (record.Actor is not null)
            {
                writer.WriteStartObject("actor");
                writer.WriteString("type", record.Actor.Type.ToString());
                writer.WriteString("id", record.Actor.Id);
                writer.WriteEndObject();
            }

            writer.WriteString("sourceService", record.SourceService);
            writer.WriteString("occurredAt", record.OccurredAt);
            writer.WriteString("recordedAt", record.RecordedAt);
            WriteOptional(writer, "correlationId", record.CorrelationId);
            WriteOptional(writer, "causationId", record.CausationId);

            if (record.Resource is not null)
            {
                writer.WriteStartObject("resource");
                writer.WriteString("type", record.Resource.Type);
                writer.WriteString("id", record.Resource.Id);
                writer.WriteEndObject();
            }

            if (record.Metadata is not null)
            {
                writer.WritePropertyName("metadata");
                writer.WriteRawValue(record.Metadata.Json);
            }

            WriteOptional(writer, "integrityHash", record.IntegrityHash);
            writer.WriteEndObject();

            count++;
            if (writer.BytesPending > 64 * 1024) await writer.FlushAsync(cancellationToken);
        }

        writer.WriteEndArray();
        await writer.FlushAsync(cancellationToken);
        return count;
    }

    private static void WriteOptional(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null) writer.WriteString(name, value);
    }
}
