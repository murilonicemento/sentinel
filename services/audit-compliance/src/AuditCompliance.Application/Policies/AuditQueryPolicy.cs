namespace AuditCompliance.Application.Policies;

/// <summary>
/// Limites aplicados às consultas e exportações. Valores definidos por configuração.
/// </summary>
public sealed class AuditQueryPolicy
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;

    public int MaxQueryRangeDays { get; init; } = 90;
    public int MaxExportRangeDays { get; init; } = 366;
}

public sealed class AuditExportPolicy
{
    public int MaxRecords { get; init; } = 10_000;
    public TimeSpan FileExpiration { get; init; } = TimeSpan.FromHours(24);
    public TimeSpan ProcessingLease { get; init; } = TimeSpan.FromMinutes(10);
    public int MaxProcessingAttempts { get; init; } = 3;
}
