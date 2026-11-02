using AuditCompliance.Domain.Aggregates;

namespace AuditCompliance.Application.DTOs;

public sealed record AuditExportDTO
{
    public Guid Id { get; init; }
    public required string Format { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public long? RecordCount { get; init; }
    public string? FailureCode { get; init; }
    public DateTimeOffset From { get; init; }
    public DateTimeOffset To { get; init; }

    public static AuditExportDTO FromDomain(AuditExport export, DateTimeOffset now)
    {
        return new AuditExportDTO
        {
            Id = export.Id,
            Format = export.Format.ToString(),
            Status = export.HasExpired(now) ? "Expired" : export.Status.ToString(),
            RequestedAt = export.RequestedAt,
            CompletedAt = export.CompletedAt,
            ExpiresAt = export.ExpiresAt,
            RecordCount = export.RecordCount,
            FailureCode = export.FailureCode,
            From = export.Filter.From,
            To = export.Filter.To
        };
    }
}

public sealed record AuditExportFileDTO(Stream Content, string FileName, string ContentType);
