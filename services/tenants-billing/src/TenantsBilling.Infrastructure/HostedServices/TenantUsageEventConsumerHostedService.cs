using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TenantsBilling.Application.Services;

namespace TenantsBilling.Infrastructure.HostedServices;

public sealed class TenantUsageEventConsumerHostedService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantUsageEventConsumerHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public TenantUsageEventConsumerHostedService(
        IConfiguration configuration,
        ILogger<TenantUsageEventConsumerHostedService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = _configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            _logger.LogWarning("Kafka bootstrap servers are not configured. Tenant usage consumer will not start.");
            return;
        }

        var topic = _configuration["Kafka:UsageTopic"] ?? "tenant-usage-events";
        var groupId = _configuration["Kafka:ConsumerGroupId"] ?? "tenants-billing-consumer-group";

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config)
            .SetErrorHandler((_, error) => _logger.LogError("Kafka consumer error: {Reason}", error.Reason))
            .Build();

        consumer.Subscribe(topic);
        _logger.LogInformation("Tenant usage consumer subscribed to topic {Topic} with group {GroupId}", topic, groupId);

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

                    var usageEvent = ParseUsageEvent(consumeResult.Message.Value);
                    if (usageEvent is null)
                    {
                        _logger.LogWarning("Ignoring invalid tenant usage payload on topic {Topic}", topic);
                        consumer.Commit(consumeResult);
                        continue;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var tenantManagementService = scope.ServiceProvider.GetRequiredService<TenantManagementService>();
                    var status = await tenantManagementService.RegisterUsageAsync(
                        usageEvent.TenantId,
                        usageEvent.EventsConsumed,
                        usageEvent.AlertsTriggered,
                        usageEvent.ApiRequests,
                        usageEvent.ChannelUsage,
                        stoppingToken);

                    _logger.LogInformation(
                        "Processed tenant usage event for tenant {TenantId}. Status: {Status}",
                        usageEvent.TenantId,
                        status);

                    consumer.Commit(consumeResult);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while consuming tenant usage event.");
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    public static TenantUsageEvent? ParseUsageEvent(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            var eventPayload = JsonSerializer.Deserialize<TenantUsageEvent>(payload);
            if (eventPayload is null || eventPayload.TenantId == Guid.Empty)
            {
                return null;
            }

            return eventPayload with
            {
                EventsConsumed = Math.Max(0, eventPayload.EventsConsumed),
                AlertsTriggered = Math.Max(0, eventPayload.AlertsTriggered),
                ApiRequests = Math.Max(0, eventPayload.ApiRequests),
                ChannelUsage = Math.Max(0, eventPayload.ChannelUsage)
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record TenantUsageEvent(
    Guid TenantId,
    int EventsConsumed,
    int AlertsTriggered,
    int ApiRequests,
    int ChannelUsage);
