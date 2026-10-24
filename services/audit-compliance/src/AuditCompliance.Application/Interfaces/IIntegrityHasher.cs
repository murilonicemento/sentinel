using AuditCompliance.Domain.Aggregates;

namespace AuditCompliance.Application.Interfaces;

public interface IIntegrityHasher
{
    /// <summary>
    /// Calcula a evidência de integridade sobre a representação canônica do registro.
    /// </summary>
    string Compute(AuditRecord record);

    /// <summary>
    /// Recalcula o hash e compara com o armazenado. Retorna null quando o registro não possui hash.
    /// </summary>
    bool? Verify(AuditRecord record);
}
