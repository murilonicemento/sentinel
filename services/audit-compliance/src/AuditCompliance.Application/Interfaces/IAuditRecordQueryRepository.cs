using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.ValueObjects;

namespace AuditCompliance.Application.Interfaces;

public enum AuditRecordSortFieldEnum
{
    OccurredAt,
    RecordedAt
}

public sealed record AuditRecordSort(AuditRecordSortFieldEnum Field, bool Descending);

public interface IAuditRecordQueryRepository
{
    /// <summary>
    /// Busca paginada com ordenação determinística (campo escolhido e Id como desempate).
    /// </summary>
    Task<(IReadOnlyList<AuditRecord> Items, long TotalItems)> SearchAsync(
        AuditRecordFilter filter,
        AuditRecordSort sort,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<long> CountAsync(AuditRecordFilter filter, CancellationToken cancellationToken);

    /// <summary>
    /// Percorre os registros em ordem crescente de ocorrência, limitado a <paramref name="limit"/> itens.
    /// </summary>
    IAsyncEnumerable<AuditRecord> StreamAsync(AuditRecordFilter filter, int limit, CancellationToken cancellationToken);
}
