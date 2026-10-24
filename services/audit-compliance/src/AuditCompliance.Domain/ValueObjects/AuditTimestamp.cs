namespace AuditCompliance.Domain.ValueObjects;

public static class AuditTimestamp
{
    /// <summary>
    /// Normaliza para UTC com precisão de milissegundos, a mesma precisão persistida pelo armazenamento.
    /// Sem essa normalização o hash de integridade calculado na ingestão divergiria do registro lido.
    /// </summary>
    public static DateTimeOffset Normalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);
    }
}
