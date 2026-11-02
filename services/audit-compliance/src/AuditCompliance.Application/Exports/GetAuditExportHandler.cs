using AuditCompliance.Application.Common;
using AuditCompliance.Application.DTOs;
using AuditCompliance.Domain.Repositories;

namespace AuditCompliance.Application.Exports;

public sealed record GetAuditExportQuery(Guid TenantId, Guid ExportId);

public sealed class GetAuditExportHandler
{
    private readonly IAuditExportRepository _exportRepository;
    private readonly TimeProvider _timeProvider;

    public GetAuditExportHandler(IAuditExportRepository exportRepository, TimeProvider timeProvider)
    {
        _exportRepository = exportRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Result<AuditExportDTO>> HandleAsync(GetAuditExportQuery query,
        CancellationToken cancellationToken)
    {
        var export = await _exportRepository.GetByIdAsync(query.TenantId, query.ExportId, cancellationToken);

        if (export is null)
            return new Error(ApplicationErrorCodes.ResourceNotFound, "Export not found.", ErrorTypeEnum.NotFound);

        return AuditExportDTO.FromDomain(export, _timeProvider.GetUtcNow());
    }
}
