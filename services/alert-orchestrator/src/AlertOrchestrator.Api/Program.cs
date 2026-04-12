using System.Net;
using System.Text.Json.Serialization;
using AlertOrchestrator.Api.Middlewares;
using AlertOrchestrator.Application.Events;
using AlertOrchestrator.Infrastructure;
using AlertOrchestrator.Infrastructure.Options;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Serilog;

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

builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration)
          .Enrich.FromLogContext()
          .Enrich.WithEnvironmentName();
});

var kafkaConfig = builder.Configuration.GetSection("Kafka");
var kafkaTopicsConfig = kafkaConfig.GetSection("Topics");
var databaseConfig = builder.Configuration.GetSection("Database");
var cacheConfig = builder.Configuration.GetSection("Cache");
var idempotencyConfig = builder.Configuration.GetSection("Idempotency");

var infrastructureOptions = new AlertOrchestratorInfrastructureOptions
{
    KafkaBootstrapServers = kafkaConfig.GetValue("BootstrapServers", "localhost:9093"),
    ConsumerGroupId = kafkaConfig.GetValue("ConsumerGroupId", "alert-orchestrator-group"),
    RiskUpdatedTopic = kafkaTopicsConfig.GetValue("RiskUpdated", "risk-updated"),
    RegionIntersectedTopic = kafkaTopicsConfig.GetValue("RegionIntersected", "region-intersected"),
    EventTopic = kafkaTopicsConfig.GetValue("Event", "alert-orchestrator-event"),
    CommandTopic = kafkaTopicsConfig.GetValue("Command", "alert-orchestrator-command"),
    UsePostgreSql = databaseConfig.GetValue("UsePostgreSql", false),
    PostgreSqlConnectionString = databaseConfig.GetValue("PostgreSqlConnectionString", string.Empty),
    UseRedis = cacheConfig.GetValue("UseRedis", false),
    RedisConnectionString = cacheConfig.GetValue("RedisConnectionString", string.Empty),
    IdempotencyExpiration = TimeSpan.FromHours(idempotencyConfig.GetValue("ExpirationHours", 24)),
    ExpirationCheckInterval = TimeSpan.FromMinutes(idempotencyConfig.GetValue("CheckIntervalMinutes", 1))
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