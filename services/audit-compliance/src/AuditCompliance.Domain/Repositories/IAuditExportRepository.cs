using AuditCompliance.Domain.Aggregates;

namespace AuditCompliance.Domain.Repositories;

public interface IAuditExportRepository
{
    Task AddAsync(AuditExport export, CancellationToken cancellationToken);

    Task<AuditExport?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Reivindica atomicamente a próxima exportação pendente (ou com lease vencido) para processamento,
    /// incrementando o número de tentativas. Seguro para múltiplas instâncias.
    /// </summary>
    Task<AuditExport?> TryClaimNextAsync(DateTimeOffset now, DateTimeOffset leaseUntil, int maxAttempts,
        CancellationToken cancellationToken);

    /// <summary>
    /// Persiste o resultado do processamento somente se a exportação ainda pertence à tentativa reivindicada.
    /// Retorna false quando outra instância assumiu a exportação.
    /// </summary>
    Task<bool> SaveProcessingResultAsync(AuditExport export, CancellationToken cancellationToken);

    /// <summary>
    /// Marca como falhas as exportações abandonadas que já esgotaram as tentativas.
    /// </summary>
    Task<long> FailAbandonedAsync(DateTimeOffset now, int maxAttempts, string failureCode,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditExport>> GetExpiredCompletedAsync(DateTimeOffset now, int limit,
        CancellationToken cancellationToken);

    Task MarkExpiredAsync(Guid id, CancellationToken cancellationToken);
}
