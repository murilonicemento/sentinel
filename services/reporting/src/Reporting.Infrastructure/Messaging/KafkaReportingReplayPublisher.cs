using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Reporting.Application.Interfaces;
using Reporting.Application.Models;
using Reporting.Infrastructure.HostedServices;

namespace Reporting.Infrastructure.Messaging;

public sealed class KafkaReportingReplayPublisher : IReportingReplayPublisher
{
    private readonly IConfiguration _configuration;

    public KafkaReportingReplayPublisher(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<ReplayPublishResult> PublishAsync(
        string sourceTopic,
        string payload,
        string requestedBy,
        CancellationToken cancellationToken = default)
    {
        var replayTopic = _configuration["Kafka:ReplayTopic"] ?? "reporting-events-replay";
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            EnableIdempotence = true,
            Acks = Acks.All,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000
        };

        using var producer = new ProducerBuilder<Null, string>(producerConfig).Build();
        var messageBody = ReportingKafkaConsumerHostedService.BuildReprocessingMessage(payload, sourceTopic, requestedBy);

        var result = await producer.ProduceAsync(replayTopic, new Message<Null, string>
        {
            Value = messageBody,
            Headers = new Headers
            {
                { "source-topic", Encoding.UTF8.GetBytes(sourceTopic) },
                { "replay-requested-at", Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("o")) },
                { "replay-requested-by", Encoding.UTF8.GetBytes(requestedBy) }
            }
        }, cancellationToken);

        return new ReplayPublishResult(replayTopic, result.Partition.Value, result.Offset.Value, sourceTopic,
            requestedBy);
    }
}
