using AlertOrchestrator.Application.Events;
using AlertOrchestrator.Infrastructure;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RiskUpdatedEvent).Assembly);
});

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
    IdempotencyExpiration = TimeSpan.FromHours(int.TryParse(alertOrchestratorConfig["IdempotencyExpirationHours"], out var hours) ? hours : 24),
    ExpirationCheckInterval = TimeSpan.FromMinutes(int.TryParse(alertOrchestratorConfig["ExpirationCheckIntervalMinutes"], out var mins) ? mins : 1)
};

builder.Services.AddAlertOrchestratorInfrastructure(infrastructureOptions);

builder.Services.AddHealthChecks();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("AlertOrchestrator");
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
