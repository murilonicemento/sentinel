using System.Net;
using System.Text.Json.Serialization;
using AlertOrchestrator.Api.Middlewares;
using AlertOrchestrator.Application.Events;
using AlertOrchestrator.Infrastructure;
using AlertOrchestrator.Infrastructure.Options;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddOpenApi();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();

        var responseObj = new
        {
            title = "One or more validation errors occurred.",
            type = "RequestFormat",
            statusCode = HttpStatusCode.BadRequest,
            success = false,
            errors = new
            {
                messages = errors
            }
        };

        return new BadRequestObjectResult(responseObj);
    };
});
builder.Services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(RiskUpdatedEvent).Assembly); });

var alertOrchestratorConfig = builder.Configuration.GetSection("AlertOrchestrator");
var infrastructureOptions = new AlertOrchestratorInfrastructureOptions
{
    KafkaBootstrapServers = alertOrchestratorConfig["KafkaBootstrapServers"] ?? "localhost:9092",
    ConsumerGroupId = alertOrchestratorConfig["ConsumerGroupId"] ?? "alert-orchestrator-group",
    RiskUpdatedTopic = alertOrchestratorConfig["RiskUpdatedTopic"] ?? "risk-updated",
    RegionIntersectedTopic = alertOrchestratorConfig["RegionIntersectedTopic"] ?? "region-intersected",
    EventTopic = alertOrchestratorConfig["EventTopic"] ?? "alert-orchestrator-events",
    CommandTopic = alertOrchestratorConfig["CommandTopic"] ?? "alert-commands",
    UsePostgreSql = bool.TryParse(alertOrchestratorConfig["UsePostgreSql"], out var usePostgreSql) && usePostgreSql,
    PostgreSqlConnectionString = alertOrchestratorConfig["PostgreSqlConnectionString"] ?? string.Empty,
    UseRedis = bool.TryParse(alertOrchestratorConfig["UseRedis"], out var useRedis) && useRedis,
    RedisConnectionString = alertOrchestratorConfig["RedisConnectionString"] ?? string.Empty,
    IdempotencyExpiration =
        TimeSpan.FromHours(int.TryParse(alertOrchestratorConfig["IdempotencyExpirationHours"], out var hours)
            ? hours
            : 24),
    ExpirationCheckInterval =
        TimeSpan.FromMinutes(int.TryParse(alertOrchestratorConfig["ExpirationCheckIntervalMinutes"], out var mins)
            ? mins
            : 1)
};

builder.Services.AddAlertOrchestratorInfrastructure(infrastructureOptions);

builder.Services.AddHealthChecks();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => { tracing.AddSource("AlertOrchestrator"); });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Sentinel - Alert Orchestrator API")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseHttpsRedirection();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();