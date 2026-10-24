namespace AlertOrchestrator.Infrastructure.Options;

public sealed class AlertOrchestratorInfrastructureOptions
{
    public bool UsePostgreSql { get; set; } = false;
    public string PostgreSqlConnectionString { get; set; } = string.Empty;

    public bool UseRedis { get; set; } = false;
    public string RedisConnectionString { get; set; } = string.Empty;

    public string KafkaBootstrapServers { get; set; } = "localhost:9092";
    public string ConsumerGroupId { get; set; } = "alert-orchestrator-group";
    public string RiskUpdatedTopic { get; set; } = "risk-updated";
    public string RegionIntersectedTopic { get; set; } = "region-intersected";
    public string EventTopic { get; set; } = "alert-orchestrator-events";
    public string CommandTopic { get; set; } = "alert-commands";

    public TimeSpan IdempotencyExpiration { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan? ExpirationCheckInterval { get; set; }
    public TimeSpan? EscalationCheckInterval { get; set; }

    public string? TenantsBillingBaseUrl { get; set; } = "http://localhost:5055";
}