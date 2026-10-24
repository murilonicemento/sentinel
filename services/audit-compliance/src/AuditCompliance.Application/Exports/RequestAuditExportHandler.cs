using AuditCompliance.Application.Common;
using AuditCompliance.Application.DTOs;
using AuditCompliance.Application.Interfaces;
using AuditCompliance.Application.Policies;
using AuditCompliance.Application.Records;
using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Catalog;
using AuditCompliance.Domain.Enums;
using AuditCompliance.Domain.Repositories;
using AuditCompliance.Domain.ValueObjects;

namespace AuditCompliance.Application.Exports;

public sealed record RequestAuditExportCommand
{
    public Guid TenantId { get; init; }
    public required string RequestedBy { get; init; }
    public string? Format { get; init; }
    public required AuditFilterInput Filter { get; init; }
    public string? CorrelationId { get; init; }
}

public sealed class RequestAuditExportHandler
{
    private readonly IAuditExportRepository _exportRepository;
    private readonly AuditExportPolicy _exportPolicy;
    private readonly AuditQueryPolicy _queryPolicy;
    private readonly IAuditRecordQueryRepository _queryRepository;
    private readonly SelfAuditRecorder _selfAuditRecorder;
    private readonly TimeProvider _timeProvider;

    public RequestAuditExportHandler(
        IAuditExportRepository exportRepository,
        IAuditRecordQueryRepository queryRepository,
        SelfAuditRecorder selfAuditRecorder,
        AuditQueryPolicy queryPolicy,
        AuditExportPolicy exportPolicy,
        TimeProvider timeProvider)
    {
        _exportRepository = exportRepository;
        _queryRepository = queryRepository;
        _selfAuditRecorder = selfAuditRecorder;
        _queryPolicy = queryPolicy;
        _exportPolicy = exportPolicy;
        _timeProvider = timeProvider;
    }

    public async Task<Result<AuditExportDTO>> HandleAsync(RequestAuditExportCommand command,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        ExportFormatEnum format = default;

        if (string.IsNullOrWhiteSpace(command.Format) ||
            !Enum.TryParse(command.Format, true, out format) ||
            !Enum.GetNames<ExportFormatEnum>().Contains(command.Format, StringComparer.OrdinalIgnoreCase))
            errors["format"] = ["'format' must be 'csv' or 'json'."];

        var filter = AuditFilterValidator.Validate(command.TenantId, command.Filter, _queryPolicy.MaxExportRangeDays,
            errors);

        if (!filter.IsSuccess) return filter.Error!;

        var matchingRecords = await _queryRepository.CountAsync(filter.Value!, cancellationToken);

        if (matchingRecords > _exportPolicy.MaxRecords)
            return new Error(ApplicationErrorCodes.ExportLimitExceeded,
                $"The filter matches {matchingRecords} records; the export limit is {_exportPolicy.MaxRecords}. Narrow the filter.",
                ErrorTypeEnum.Unprocessable);

        var now = _timeProvider.GetUtcNow();
        var export = AuditExport.Request(filter.Value!, format, command.RequestedBy, now);

        await _selfAuditRecorder.RecordAsync(
            AuditEventCatalog.AuditExportRequested,
            $"export:{export.Id:N}:requested",
            export.TenantId,
            export.RequestedBy,
            ResourceReference.Create("AuditExport", export.Id.ToString()),
            new
            {
                format = format.ToString(),
                from = export.Filter.From,
                to = export.Filter.To,
                eventType = export.Filter.EventType,
                matchingRecords
            },
            command.CorrelationId,
            cancellationToken);

        await _exportRepository.AddAsync(export, cancellationToken);

        return AuditExportDTO.FromDomain(export, now);
    }
}
