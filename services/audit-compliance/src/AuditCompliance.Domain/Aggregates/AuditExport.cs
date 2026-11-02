using AuditCompliance.Domain.Enums;
using AuditCompliance.Domain.Exceptions;
using AuditCompliance.Domain.ValueObjects;

namespace AuditCompliance.Domain.Aggregates;

/// <summary>
/// Solicitação de exportação assíncrona de registros de auditoria de um tenant.
/// O arquivo gerado possui prazo de validade; após expirar ele não pode mais ser baixado.
/// </summary>
public sealed class AuditExport
{
    public const int MaxRequesterLength = 128;

    private AuditExport(AuditRecordFilter filter, string requestedBy)
    {
        Filter = filter;
        RequestedBy = requestedBy;
    }

    public Guid Id { get; private init; }
    public Guid TenantId => Filter.TenantId;
    public AuditRecordFilter Filter { get; }
    public ExportFormatEnum Format { get; private init; }
    public string RequestedBy { get; }
    public ExportStatusEnum Status { get; private set; }
    public DateTimeOffset RequestedAt { get; private init; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public int Attempts { get; private init; }
    public string? FileId { get; private set; }
    public long? RecordCount { get; private set; }
    public string? FailureCode { get; private set; }

    public static AuditExport Request(AuditRecordFilter filter, ExportFormatEnum format, string? requestedBy,
        DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (!Enum.IsDefined(format))
            throw new AuditDomainException(AuditErrorCodes.InvalidField, "Export format is not supported.");

        if (string.IsNullOrWhiteSpace(requestedBy) || requestedBy.Length > MaxRequesterLength)
            throw new AuditDomainException(AuditErrorCodes.InvalidActor, "Export requester is required.");

        return new AuditExport(filter, requestedBy.Trim())
        {
            Id = Guid.NewGuid(),
            Format = format,
            Status = ExportStatusEnum.Pending,
            RequestedAt = AuditTimestamp.Normalize(requestedAt)
        };
    }

    public static AuditExport Rehydrate(
        Guid id,
        AuditRecordFilter filter,
        ExportFormatEnum format,
        string requestedBy,
        ExportStatusEnum status,
        DateTimeOffset requestedAt,
        DateTimeOffset? completedAt,
        DateTimeOffset? expiresAt,
        int attempts,
        string? fileId,
        long? recordCount,
        string? failureCode)
    {
        return new AuditExport(filter, requestedBy)
        {
            Id = id,
            Format = format,
            Status = status,
            RequestedAt = requestedAt,
            CompletedAt = completedAt,
            ExpiresAt = expiresAt,
            Attempts = attempts,
            FileId = fileId,
            RecordCount = recordCount,
            FailureCode = failureCode
        };
    }

    public string FileName => $"audit-export-{Id:N}.{(Format == ExportFormatEnum.Csv ? "csv" : "json")}";

    public void Complete(string fileId, long recordCount, DateTimeOffset completedAt, DateTimeOffset expiresAt)
    {
        EnsureStatus(ExportStatusEnum.Processing);

        if (string.IsNullOrWhiteSpace(fileId))
            throw new AuditDomainException(AuditErrorCodes.InvalidField, "File id is required.");

        if (expiresAt <= completedAt)
            throw new AuditDomainException(AuditErrorCodes.InvalidField, "Expiration must be after completion.");

        Status = ExportStatusEnum.Completed;
        FileId = fileId;
        RecordCount = recordCount;
        CompletedAt = AuditTimestamp.Normalize(completedAt);
        ExpiresAt = AuditTimestamp.Normalize(expiresAt);
    }

    public void Fail(string failureCode, DateTimeOffset failedAt)
    {
        EnsureStatus(ExportStatusEnum.Processing);

        Status = ExportStatusEnum.Failed;
        FailureCode = failureCode;
        CompletedAt = AuditTimestamp.Normalize(failedAt);
    }

    public bool IsAvailableForDownload(DateTimeOffset now)
    {
        return Status == ExportStatusEnum.Completed && FileId is not null && ExpiresAt > now;
    }

    public bool HasExpired(DateTimeOffset now)
    {
        return Status == ExportStatusEnum.Expired || (Status == ExportStatusEnum.Completed && ExpiresAt <= now);
    }

    private void EnsureStatus(ExportStatusEnum expected)
    {
        if (Status != expected)
            throw new AuditDomainException(AuditErrorCodes.InvalidExportState,
                $"Export must be in status {expected} but is {Status}.");
    }
}
