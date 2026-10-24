using System.Text.Json;
using AuditCompliance.Application.Interfaces;
using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Enums;
using AuditCompliance.Domain.Repositories;
using AuditCompliance.Domain.ValueObjects;

namespace AuditCompliance.Application.Exports;

/// <summary>
/// Registra na própria trilha de auditoria os acessos sensíveis realizados por este serviço
/// (solicitação e download de exportações). O registro é gravado antes do acesso ser concedido.
/// </summary>
public sealed class SelfAuditRecorder
{
    public const string ServiceName = "audit-compliance";

    private readonly IIntegrityHasher _integrityHasher;
    private readonly IAuditRecordRepository _repository;
    private readonly TimeProvider _timeProvider;

    public SelfAuditRecorder(IAuditRecordRepository repository, IIntegrityHasher integrityHasher,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _integrityHasher = integrityHasher;
        _timeProvider = timeProvider;
    }

    public async Task RecordAsync(
        string eventType,
        string eventId,
        Guid tenantId,
        string actorId,
        ResourceReference resource,
        object metadata,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var record = AuditRecord.Create(
            eventId,
            eventType,
            1,
            ServiceName,
            now,
            now,
            tenantId,
            eventType,
            Actor.Create(ActorTypeEnum.User, actorId),
            correlationId,
            resource: resource,
            metadata: AuditPayload.FromJson(JsonSerializer.Serialize(metadata)));

        record.SealIntegrity(_integrityHasher.Compute(record));

        await _repository.AppendAsync(record, cancellationToken);
    }
}
