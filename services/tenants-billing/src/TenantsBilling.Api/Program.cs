using Confluent.Kafka;
using TenantsBilling.Application.Services;
using TenantsBilling.Domain.Events;
using TenantsBilling.Domain.Repositories;
using TenantsBilling.Infrastructure;
using TenantsBilling.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var postgresConnectionString = builder.Configuration.GetConnectionString("TenantsBillingDatabase");
var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
var kafkaTopic = builder.Configuration["Kafka:Topic"] ?? "tenants-billing-events";

builder.Services.AddTenantsBillingInfrastructure(postgresConnectionString, usePostgres: !string.IsNullOrWhiteSpace(postgresConnectionString));

builder.Services.AddSingleton<IProducer<Null, string>>(_ =>
    new ProducerBuilder<Null, string>(new ProducerConfig { BootstrapServers = kafkaBootstrapServers }).Build());

builder.Services.AddSingleton<ITenantEventPublisher>(sp =>
    new KafkaTenantEventPublisher(sp.GetRequiredService<IProducer<Null, string>>(), kafkaTopic));

builder.Services.AddScoped<TenantManagementService>();
builder.Services.AddScoped<PlanManagementService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "tenants-billing" }));

app.MapPost("/api/v1/tenants", async (CreateTenantCommand command, TenantManagementService service) =>
{
    var tenant = await service.CreateTenantAsync(command);
    return Results.Created($"/api/v1/tenants/{tenant.Id}", new
    {
        tenant.Id,
        tenant.Name,
        tenant.Status,
        tenant.PlanId,
        tenant.Region,
        tenant.TimeZone
    });
});

app.MapGet("/api/v1/tenants/{tenantId:guid}", async (Guid tenantId, ITenantRepository repository) =>
{
    var tenant = await repository.GetByIdAsync(tenantId);
    return tenant is null ? Results.NotFound() : Results.Ok(tenant);
});

app.MapPost("/api/v1/tenants/{tenantId:guid}/usage", async (Guid tenantId, UsageRequest request, TenantManagementService service) =>
{
    var status = await service.RegisterUsageAsync(
        tenantId,
        request.EventsConsumed,
        request.AlertsTriggered,
        request.ApiRequests,
        request.ChannelUsage);

    return Results.Ok(new { tenantId, status });
});

app.MapPost("/api/v1/plans", async (CreatePlanCommand command, PlanManagementService service) =>
{
    var plan = await service.CreatePlanAsync(command);
    return Results.Created($"/api/v1/plans/{plan.Id}", new { plan.Id, plan.Name, plan.MaxEventsPerMonth, plan.MaxAlertsPerMonth });
});

app.Run();

public sealed record UsageRequest(
    int EventsConsumed,
    int AlertsTriggered,
    int ApiRequests,
    int ChannelUsage);
