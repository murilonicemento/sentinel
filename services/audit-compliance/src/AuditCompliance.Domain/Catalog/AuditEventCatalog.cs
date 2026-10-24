namespace AuditCompliance.Domain.Catalog;

/// <summary>
/// Catálogo de eventos auditáveis aceitos e a maior versão de contrato suportada por cada um.
/// Eventos fora do catálogo ou com versão superior são rejeitados e encaminhados para dead-letter.
/// </summary>
public static class AuditEventCatalog
{
    public const string TenantCreated = "TenantCreated";
    public const string TenantUpdated = "TenantUpdated";
    public const string PlanChanged = "PlanChanged";
    public const string AlertTriggered = "AlertTriggered";
    public const string AlertDispatched = "AlertDispatched";
    public const string NotificationSent = "NotificationSent";
    public const string NotificationFailed = "NotificationFailed";
    public const string RiskUpdated = "RiskUpdated";

    // Eventos emitidos pelo próprio serviço ao gerar e baixar exportações.
    public const string AuditExportRequested = "AuditExportRequested";
    public const string AuditExportDownloaded = "AuditExportDownloaded";

    private static readonly IReadOnlyDictionary<string, int> MaxSupportedVersions =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [TenantCreated] = 1,
            [TenantUpdated] = 1,
            [PlanChanged] = 1,
            [AlertTriggered] = 1,
            [AlertDispatched] = 1,
            [NotificationSent] = 1,
            [NotificationFailed] = 1,
            [RiskUpdated] = 1,
            [AuditExportRequested] = 1,
            [AuditExportDownloaded] = 1
        };

    public static IReadOnlyCollection<string> EventTypes => MaxSupportedVersions.Keys.ToList();

    public static bool IsKnown(string? eventType)
    {
        return eventType is not null && MaxSupportedVersions.ContainsKey(eventType);
    }

    public static bool IsVersionSupported(string eventType, int version)
    {
        return MaxSupportedVersions.TryGetValue(eventType, out var maxVersion) && version >= 1 && version <= maxVersion;
    }
}
