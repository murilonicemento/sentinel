using System.Text.Json;
using System.Text.Json.Serialization;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Entities;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Infrastructure.HostedServices;

public sealed class NotificationConsumerHostedService : BackgroundService
{
    private readonly IConfiguration _configuration;

    private readonly ILogger<NotificationConsumerHostedService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public NotificationConsumerHostedService(
        IConfiguration configuration,
        ILogger<NotificationConsumerHostedService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _serviceScopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = _configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            _logger.LogWarning(
                "Kafka bootstrap servers are not configured. NotificationConsumerHostedService will not start.");
            return;
        }

        var topic = _configuration["Kafka:Topic"] ?? "notification-events";
        var groupId = _configuration["Kafka:ConsumerGroupId"] ?? "channels-service-group";

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            AllowAutoCreateTopics = true
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config)
            .SetErrorHandler((_, error) => _logger.LogError("Kafka consumer error: {Reason}", error.Reason))
            .Build();

        consumer.Subscribe(topic);
        _logger.LogInformation("Kafka consumer subscribed to topic {Topic} using group {GroupId}.", topic, groupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(stoppingToken);
                    if (consumeResult?.Message?.Value is null)
                    {
                        continue;
                    }

                    var notification = JsonSerializer.Deserialize<NotificationEvent>(consumeResult.Message.Value,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            Converters = { new JsonStringEnumConverter() }
                        });

                    if (notification == null)
                    {
                        _logger.LogWarning("Received invalid notification payload on topic {Topic}.", topic);
                        continue;
                    }

                    _logger.LogInformation(
                        "Received notification event {EventId} type {EventType} correlation {CorrelationId} from Kafka.",
                        notification.EventId,
                        notification.EventType,
                        notification.CorrelationId);

                    using var scope = _serviceScopeFactory.CreateScope();
                    var deliveryService =
                        scope.ServiceProvider.GetRequiredService<IChannelDeliveryService>();
                    var deadLetterPublisher =
                        scope.ServiceProvider.GetRequiredService<IDeadLetterPublisher>();
                    var deliveryResult = await deliveryService.DeliverAsync(notification, stoppingToken);

                    if (deliveryResult.Success) continue;

                    _logger.LogError(
                        "Notification event {EventId} failed processing: {Error}",
                        notification.EventId,
                        deliveryResult.Error);

                    await deadLetterPublisher.PublishAsync(notification, deliveryResult, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Error consuming notification event.");
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }
}