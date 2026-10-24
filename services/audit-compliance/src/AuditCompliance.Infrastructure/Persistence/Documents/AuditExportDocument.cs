using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Enums;
using AuditCompliance.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuditCompliance.Infrastructure.Persistence.Documents;

[BsonIgnoreExtraElements]
public sealed class AuditExportDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid TenantId { get; set; }

    public ExportFilterDocument Filter { get; set; } = new();
    public string Format { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime RequestedAt { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CompletedAt { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ExpiresAt { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LeaseExpiresAt { get; set; }

    public int Attempts { get; set; }
    public string? FileId { get; set; }
    public long? RecordCount { get; set; }
    public string? FailureCode { get; set; }

    public static AuditExportDocument FromDomain(AuditExport export)
    {
        return new AuditExportDocument
        {
            Id = export.Id,
            TenantId = export.TenantId,
            Filter = ExportFilterDocument.FromDomain(export.Filter),
            Format = export.Format.ToString(),
            Status = export.Status.ToString(),
            RequestedBy = export.RequestedBy,
            RequestedAt = export.RequestedAt.UtcDateTime,
            CompletedAt = export.CompletedAt?.UtcDateTime,
            ExpiresAt = export.ExpiresAt?.UtcDateTime,
            Attempts = export.Attempts,
            FileId = export.FileId,
            RecordCount = export.RecordCount,
            FailureCode = export.FailureCode
        };
    }

    public AuditExport ToDomain()
    {
        return AuditExport.Rehydrate(
            Id,
            Filter.ToDomain(TenantId),
            Enum.Parse<ExportFormatEnum>(Format),
            RequestedBy,
            Enum.Parse<ExportStatusEnum>(Status),
            ToOffset(RequestedAt),
            CompletedAt is null ? null : ToOffset(CompletedAt.Value),
            ExpiresAt is null ? null : ToOffset(ExpiresAt.Value),
            Attempts,
            FileId,
            RecordCount,
            FailureCode);
    }

    private static DateTimeOffset ToOffset(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}

public sealed class ExportFilterDocument
{
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime From { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime To { get; set; }

    public string? EventType { get; set; }
    public string? ActorId { get; set; }
    public string? SourceService { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string? CorrelationId { get; set; }

    public static ExportFilterDocument FromDomain(AuditRecordFilter filter)
    {
        return new ExportFilterDocument
        {
            From = filter.From.UtcDateTime,
            To = filter.To.UtcDateTime,
            EventType = filter.EventType,
            ActorId = filter.ActorId,
            SourceService = filter.SourceService,
            ResourceType = filter.ResourceType,
            ResourceId = filter.ResourceId,
            CorrelationId = filter.CorrelationId
        };
    }

    public AuditRecordFilter ToDomain(Guid tenantId)
    {
        return AuditRecordFilter.Create(
            tenantId,
            new DateTimeOffset(DateTime.SpecifyKind(From, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(To, DateTimeKind.Utc)),
            EventType,
            ActorId,
            SourceService,
            ResourceType,
            ResourceId,
            CorrelationId);
    }
}
