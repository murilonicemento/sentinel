using AuditCompliance.Domain.Aggregates;

namespace AuditCompliance.Domain.Repositories;

/// <summary>
/// Repositório append-only: não há operações de atualização ou exclusão de registros.
/// </summary>
public interface IAuditRecordRepository
{
    /// <summary>
    /// Persiste o registro de forma durável. Retorna <see cref="AppendResultEnum.Duplicate"/> quando já existe
    /// um registro com o mesmo par (SourceService, EventId), garantido por restrição de unicidade.
    /// </summary>
    Task<AppendResultEnum> AppendAsync(AuditRecord record, CancellationToken cancellationToken);

    Task<AuditRecord?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
}

public enum AppendResultEnum
{
    Appended,
    Duplicate
}
