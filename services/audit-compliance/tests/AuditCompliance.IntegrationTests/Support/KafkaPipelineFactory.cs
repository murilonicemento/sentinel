using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Testcontainers.Kafka;

namespace AuditCompliance.IntegrationTests.Support;

/// <summary>
/// API com MongoDB e Kafka reais e o consumidor de auditoria habilitado.
/// </summary>
public sealed class KafkaPipelineFactory : AuditComplianceApiFactory
{
    public const string AuditTopic = "audit-events";
    public const string ReplayTopic = "audit-events-replay";
    public const string DeadLetterTopic = "audit-events-dlq";

    private readonly KafkaContainer _kafka = new KafkaBuilder().WithImage("confluentinc/cp-kafka:7.6.0").Build();

    public string BootstrapServers => _kafka.GetBootstrapAddress();

    protected override async Task StartContainersAsync()
    {
        await Task.WhenAll(base.StartContainersAsync(), _kafka.StartAsync());

        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = BootstrapServers }).Build();
        await admin.CreateTopicsAsync(new[] { AuditTopic, ReplayTopic, DeadLetterTopic }.Select(topic =>
            new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }));
    }

    protected override async Task StopContainersAsync()
    {
        await base.StopContainersAsync();
        await _kafka.DisposeAsync();
    }

    protected override IEnumerable<KeyValuePair<string, string>> Settings()
    {
        var settings = base.Settings().ToDictionary();
        settings["Kafka:ConsumerEnabled"] = "true";
        settings["Kafka:BootstrapServers"] = BootstrapServers;
        settings["Kafka:ConsumerGroupId"] = $"audit-tests-{Guid.NewGuid():N}";
        settings["Kafka:InitialRetryDelayMilliseconds"] = "50";
        settings["Kafka:StorageUnavailablePauseSeconds"] = "1";
        settings["MongoDb:ServerSelectionTimeoutSeconds"] = "2";
        settings["MongoDb:SocketTimeoutSeconds"] = "2";
        return settings;
    }

    public async Task PublishAsync(string topic, string key, string payload)
    {
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = BootstrapServers,
            Acks = Acks.All
        }).Build();

        await producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = payload });
    }
}
