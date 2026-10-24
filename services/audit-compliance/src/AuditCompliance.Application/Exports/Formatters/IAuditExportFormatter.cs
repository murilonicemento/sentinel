using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Enums;

namespace AuditCompliance.Application.Exports.Formatters;

/// <summary>
/// Serializa registros para exportação. Os estados anterior/posterior nunca são exportados.
/// </summary>
public interface IAuditExportFormatter
{
    ExportFormatEnum Format { get; }
    string ContentType { get; }

    Task<long> WriteAsync(IAsyncEnumerable<AuditRecord> records, Stream output, CancellationToken cancellationToken);
}
