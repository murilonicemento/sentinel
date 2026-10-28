using System.Security.Claims;
using TenantsBilling.Api.Contracts;
using Scalar.AspNetCore;
using TenantsBilling.Api.Endpoints;
using TenantsBilling.Api.Observability;
using TenantsBilling.Api.Security;
using TenantsBilling.Application.Services;
using TenantsBilling.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddSentinelObservability("tenants-billing");
builder.Services.AddOpenApi(options => options.MarkDeprecatedOperations());
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var status = context.ProblemDetails.Status ?? StatusCodes.Status500InternalServerError;
        ApiProblems.Enrich(context.ProblemDetails, context.HttpContext, ApiProblems.DefaultCodeFor(status));
    };
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddTenantsBillingAuthentication(builder.Configuration);

var postgresConnectionString = builder.Configuration.GetConnectionString("TenantsBillingDatabase");
var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"]
    ?? throw new InvalidOperationException("Configuration 'Kafka:BootstrapServers' is required.");
var kafkaTopic = builder.Configuration["Kafka:Topic"] ?? "tenants-billing-events";

builder.Services.AddTenantsBillingInfrastructure(
    postgresConnectionString,
    kafkaBootstrapServers,
    kafkaTopic,
    usePostgres: !string.IsNullOrWhiteSpace(postgresConnectionString));

builder.Services.AddScoped<TenantManagementService>();
builder.Services.AddScoped<PlanManagementService>();

if (!string.IsNullOrWhiteSpace(postgresConnectionString))
    builder.Services.AddHealthChecks().AddNpgSql(postgresConnectionString, name: "postgresql",
        tags: [ObservabilityExtensions.ReadinessTag], timeout: TimeSpan.FromSeconds(3));

var app = builder.Build();

app.UseSentinelRequestLogging();
app.UseExceptionHandler();
app.UseWhen(ApiProblems.IsVersionedApi, versioned => versioned.UseStatusCodePages());
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options => options.WithTitle("Sentinel - Tenants & Billing API")).AllowAnonymous();
}

app.MapSentinelOperationalEndpoints();

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
}).RequireAuthorization(TenantsBillingPolicies.Write)
  .MarkDeprecated(TenantsBillingV1Endpoints.BasePath + "/tenants");

app.MapGet("/api/v1/tenants/{tenantId:guid}", async (
    Guid tenantId,
    ClaimsPrincipal user,
    TenantAccessEvaluator tenantAccess,
    TenantManagementService service) =>
{
    if (!tenantAccess.CanAccessTenant(user, tenantId)) return Results.Forbid();

    var tenant = await service.GetTenantAsync(tenantId);
    return tenant is null ? Results.NotFound() : Results.Ok(tenant);
}).RequireAuthorization(TenantsBillingPolicies.Read)
  .MarkDeprecated(TenantsBillingV1Endpoints.BasePath + "/tenants/{tenantId}");

app.MapPost("/api/v1/tenants/{tenantId:guid}/usage", async (
    Guid tenantId,
    UsageRequest request,
    ClaimsPrincipal user,
    TenantAccessEvaluator tenantAccess,
    TenantManagementService service) =>
{
    if (!tenantAccess.CanAccessTenant(user, tenantId)) return Results.Forbid();

    var status = await service.RegisterUsageAsync(
        tenantId,
        request.EventsConsumed,
        request.AlertsTriggered,
        request.ApiRequests,
        request.ChannelUsage);

    return Results.Ok(new { tenantId, status });
}).RequireAuthorization(TenantsBillingPolicies.Write)
  .MarkDeprecated(TenantsBillingV1Endpoints.BasePath + "/tenants/{tenantId}/usage");

app.MapPost("/api/v1/plans", async (CreatePlanCommand command, PlanManagementService service) =>
{
    var plan = await service.CreatePlanAsync(command);
    return Results.Created($"/api/v1/plans/{plan.Id}", new { plan.Id, plan.Name, plan.MaxEventsPerMonth, plan.MaxAlertsPerMonth });
}).RequireAuthorization(TenantsBillingPolicies.Write)
  .MarkDeprecated(TenantsBillingV1Endpoints.BasePath + "/plans");

app.MapTenantsBillingV1Endpoints();

app.Run();

public partial class Program;

public sealed record UsageRequest(
    int EventsConsumed,
    int AlertsTriggered,
    int ApiRequests,
    int ChannelUsage);
