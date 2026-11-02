using System.Globalization;
using System.Text;
using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Enums;

namespace AuditCompliance.Application.Exports.Formatters;

public sealed class CsvAuditExportFormatter : IAuditExportFormatter
{
    private static readonly string[] Header =
    [
        "id", "tenantId", "eventId", "eventType", "eventVersion", "action", "actorType", "actorId", "sourceService",
        "occurredAt", "recordedAt", "correlationId", "causationId", "resourceType", "resourceId", "metadata",
        "integrityHash"
    ];

    public ExportFormatEnum Format => ExportFormatEnum.Csv;
    public string ContentType => "text/csv";

    public async Task<long> WriteAsync(IAsyncEnumerable<AuditRecord> records, Stream output,
        CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        await writer.WriteLineAsync(string.Join(',', Header));

        long count = 0;
        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            var fields = new[]
            {
                record.Id.ToString(),
                record.TenantId?.ToString(),
                record.EventId,
                record.EventType,
                record.EventVersion.ToString(CultureInfo.InvariantCulture),
                record.Action,
                record.Actor?.Type.ToString(),
                record.Actor?.Id,
                record.SourceService,
                record.OccurredAt.ToString("O", CultureInfo.InvariantCulture),
                record.RecordedAt.ToString("O", CultureInfo.InvariantCulture),
                record.CorrelationId,
                record.CausationId,
                record.Resource?.Type,
                record.Resource?.Id,
                record.Metadata?.Json,
                record.IntegrityHash
            };

            await writer.WriteLineAsync(string.Join(',', fields.Select(Escape)));
            count++;
        }

        await writer.FlushAsync(cancellationToken);
        return count;
    }

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        // Evita injeção de fórmulas quando o arquivo é aberto em planilhas.
        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
