using ChannelsService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Confluent.Kafka;

namespace ChannelsService.Infrastructure.Messaging;

public sealed class KafkaHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<KafkaHealthCheck> _logger;

    public KafkaHealthCheck(IConfiguration configuration, ILogger<KafkaHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var bootstrapServers = _configuration["Kafka:BootstrapServers"];
        var topic = _configuration["Kafka:Topic"];

        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Kafka bootstrap servers are not configured."));
        }

        if (string.IsNullOrWhiteSpace(topic))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Kafka topic is not configured."));
        }

        try
        {
            using var adminClient = new AdminClientBuilder(new ClientConfig
            {
                BootstrapServers = bootstrapServers
            }).Build();

            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
            var topicMetadata = metadata.Topics.FirstOrDefault(t => string.Equals(t.Topic, topic, StringComparison.OrdinalIgnoreCase));
            if (topicMetadata == null)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy($"Kafka topic '{topic}' was not found."));
            }

            if (topicMetadata.Error.IsError)
            {
                _logger.LogWarning("Kafka topic {Topic} metadata returned error {Error}", topic, topicMetadata.Error.Reason);
                return Task.FromResult(HealthCheckResult.Unhealthy($"Kafka topic '{topic}' metadata error: {topicMetadata.Error.Reason}"));
            }

            return Task.FromResult(HealthCheckResult.Healthy("Kafka is reachable and topic metadata was retrieved."));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Kafka health check failed.");
            return Task.FromResult(HealthCheckResult.Unhealthy("Kafka health check exception.", exception));
        }
    }
}
