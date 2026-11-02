using AuditCompliance.Application.Common;
using AuditCompliance.Application.DTOs;
using AuditCompliance.Application.Exports.Formatters;
using AuditCompliance.Application.Interfaces;
using AuditCompliance.Domain.Catalog;
using AuditCompliance.Domain.Enums;
using AuditCompliance.Domain.Repositories;
using AuditCompliance.Domain.ValueObjects;

namespace AuditCompliance.Application.Exports;

public sealed record DownloadAuditExportQuery(Guid TenantId, Guid ExportId, string RequestedBy, string? CorrelationId);

public sealed class DownloadAuditExportHandler
{
    private readonly IAuditExportRepository _exportRepository;
    private readonly IExportFileStorage _fileStorage;
    private readonly IEnumerable<IAuditExportFormatter> _formatters;
    private readonly SelfAuditRecorder _selfAuditRecorder;
    private readonly TimeProvider _timeProvider;

    public DownloadAuditExportHandler(
        IAuditExportRepository exportRepository,
        IExportFileStorage fileStorage,
        IEnumerable<IAuditExportFormatter> formatters,
        SelfAuditRecorder selfAuditRecorder,
        TimeProvider timeProvider)
    {
        _exportRepository = exportRepository;
        _fileStorage = fileStorage;
        _formatters = formatters;
        _selfAuditRecorder = selfAuditRecorder;
        _timeProvider = timeProvider;
    }

    public async Task<Result<AuditExportFileDTO>> HandleAsync(DownloadAuditExportQuery query,
        CancellationToken cancellationToken)
    {
        var export = await _exportRepository.GetByIdAsync(query.TenantId, query.ExportId, cancellationToken);

        if (export is null)
            return new Error(ApplicationErrorCodes.ResourceNotFound, "Export not found.", ErrorTypeEnum.NotFound);

        var now = _timeProvider.GetUtcNow();

        if (export.HasExpired(now))
            return new Error(ApplicationErrorCodes.ExportExpired, "The export file has expired.", ErrorTypeEnum.Gone);

        if (export.Status == ExportStatusEnum.Failed)
            return new Error(ApplicationErrorCodes.ExportFailed, "The export could not be generated.",
                ErrorTypeEnum.Conflict);

        if (!export.IsAvailableForDownload(now))
            return new Error(ApplicationErrorCodes.ExportNotReady, "The export is still being processed.",
                ErrorTypeEnum.Conflict);

        await _selfAuditRecorder.RecordAsync(
            AuditEventCatalog.AuditExportDownloaded,
            $"export:{export.Id:N}:downloaded:{Guid.NewGuid():N}",
            export.TenantId,
            query.RequestedBy,
            ResourceReference.Create("AuditExport", export.Id.ToString()),
            new { format = export.Format.ToString(), recordCount = export.RecordCount },
            query.CorrelationId,
            cancellationToken);

        var content = await _fileStorage.OpenReadAsync(export.FileId!, cancellationToken);
        var contentType = _formatters.Single(f => f.Format == export.Format).ContentType;

        return new AuditExportFileDTO(content, export.FileName, contentType);
    }
}
