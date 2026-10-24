using System.Text.Json;
using AuditCompliance.Application.Common;
using AuditCompliance.Application.DTOs;
using AuditCompliance.Application.Interfaces;
using AuditCompliance.Domain.Repositories;
using AuditCompliance.Domain.ValueObjects;

namespace AuditCompliance.Application.Records;

public sealed record GetAuditRecordByIdQuery(Guid TenantId, Guid RecordId, bool CanReadStates);

public sealed class GetAuditRecordByIdHandler
{
    private readonly IIntegrityHasher _integrityHasher;
    private readonly IAuditRecordRepository _repository;

    public GetAuditRecordByIdHandler(IAuditRecordRepository repository, IIntegrityHasher integrityHasher)
    {
        _repository = repository;
        _integrityHasher = integrityHasher;
    }

    public async Task<Result<AuditRecordDetailDTO>> HandleAsync(GetAuditRecordByIdQuery query,
        CancellationToken cancellationToken)
    {
        var record = await _repository.GetByIdAsync(query.TenantId, query.RecordId, cancellationToken);

        if (record is null)
            return new Error(ApplicationErrorCodes.ResourceNotFound, "Audit record not found.", ErrorTypeEnum.NotFound);

        var hasStates = record.BeforeState is not null || record.AfterState is not null;

        return new AuditRecordDetailDTO
        {
            Record = AuditRecordSummaryDTO.FromDomain(record),
            Metadata = ToJson(record.Metadata),
            BeforeState = query.CanReadStates ? ToJson(record.BeforeState) : null,
            AfterState = query.CanReadStates ? ToJson(record.AfterState) : null,
            StatesRedacted = hasStates && !query.CanReadStates,
            IntegrityHash = record.IntegrityHash,
            IntegrityVerified = _integrityHasher.Verify(record),
            SchemaVersion = record.SchemaVersion
        };
    }

    private static JsonElement? ToJson(AuditPayload? payload)
    {
        if (payload is null) return null;

        using var document = JsonDocument.Parse(payload.Json);
        return document.RootElement.Clone();
    }
}
