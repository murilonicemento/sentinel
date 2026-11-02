using System.ComponentModel.DataAnnotations;

namespace AuditCompliance.Infrastructure.Options;

public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    [Required] public string ConnectionString { get; set; } = string.Empty;

    [Required] public string DatabaseName { get; set; } = "AuditComplianceDatabase";

    /// <summary>
    /// Cria os índices ao iniciar. Em produção os índices devem ser provisionados pelo procedimento de
    /// implantação (platform/init-db/mongodb) e esta opção deve permanecer desabilitada.
    /// </summary>
    public bool EnsureIndexesOnStartup { get; set; }

    [Range(1, 60)] public int ServerSelectionTimeoutSeconds { get; set; } = 5;

    [Range(1, 60)] public int SocketTimeoutSeconds { get; set; } = 30;
}

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public bool ConsumerEnabled { get; set; } = true;

    [Required] public string BootstrapServers { get; set; } = string.Empty;

    [Required] public string ConsumerGroupId { get; set; } = "audit-compliance-group";

    [Required] public string AuditEventsTopic { get; set; } = "audit-events";

    [Required] public string ReplayTopic { get; set; } = "audit-events-replay";

    [Required] public string DeadLetterTopic { get; set; } = "audit-events-dlq";

    /// <summary>Tentativas por mensagem, incluindo a primeira, antes de pausar o consumo.</summary>
    [Range(1, 3)] public int MaxProcessingAttempts { get; set; } = 3;

    [Range(10, 10_000)] public int InitialRetryDelayMilliseconds { get; set; } = 500;

    /// <summary>Pausa aplicada quando o armazenamento permanece indisponível após as tentativas.</summary>
    [Range(1, 600)] public int StorageUnavailablePauseSeconds { get; set; } = 30;
}

public sealed class AuditQueryOptions
{
    public const string SectionName = "AuditQuery";

    [Range(1, 366)] public int MaxQueryRangeDays { get; set; } = 90;

    [Range(1, 3660)] public int MaxExportRangeDays { get; set; } = 366;
}

public sealed class ExportOptions
{
    public const string SectionName = "Exports";

    public bool WorkerEnabled { get; set; } = true;

    [Range(1, 100_000)] public int MaxRecords { get; set; } = 10_000;

    [Range(1, 168)] public int FileExpirationHours { get; set; } = 24;

    [Range(1, 120)] public int ProcessingLeaseMinutes { get; set; } = 10;

    [Range(1, 3)] public int MaxProcessingAttempts { get; set; } = 3;

    [Range(1, 3600)] public int PollIntervalSeconds { get; set; } = 10;
}
