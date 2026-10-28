using System.Net;
using System.Text.Json.Serialization;
using AlertOrchestrator.Api.Contracts;
using AlertOrchestrator.Api.Middlewares;
using AlertOrchestrator.Api.Observability;
using AlertOrchestrator.Api.Security;
using AlertOrchestrator.Application.Events;
using AlertOrchestrator.Infrastructure;
using AlertOrchestrator.Infrastructure.Options;
using AlertOrchestrator.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

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
builder.Services.AddAlertOrchestratorAuthentication(builder.Configuration);
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        if (ApiProblems.IsVersionedApi(context.HttpContext)) return ModelValidationProblems.For(context);

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

builder.AddSentinelObservability("alert-orchestrator", activitySources: ["AlertOrchestrator"]);

var kafkaConfig = builder.Configuration.GetSection("Kafka");
var kafkaTopicsConfig = kafkaConfig.GetSection("Topics");
var databaseConfig = builder.Configuration.GetSection("Database");
var cacheConfig = builder.Configuration.GetSection("Cache");
var idempotencyConfig = builder.Configuration.GetSection("Idempotency");
var servicesConfig = builder.Configuration.GetSection("Services");

var infrastructureOptions = new AlertOrchestratorInfrastructureOptions
{
    KafkaBootstrapServers = kafkaConfig["BootstrapServers"]
        ?? throw new InvalidOperationException("Configuration 'Kafka:BootstrapServers' is required."),
    ConsumerGroupId = kafkaConfig.GetValue("ConsumerGroupId", "alert-orchestrator-group"),
    RiskUpdatedTopic = kafkaTopicsConfig.GetValue("RiskUpdated", "risk-updated"),
    RegionIntersectedTopic = kafkaTopicsConfig.GetValue("RegionIntersected", "region-intersected"),
    EventTopic = kafkaTopicsConfig.GetValue("Event", "alert-orchestrator-event"),
    CommandTopic = kafkaTopicsConfig.GetValue("Command", "alert-orchestrator-command"),
    DeadLetterTopic = kafkaTopicsConfig.GetValue("DeadLetter", "alert-orchestrator-dead-letter"),
    UsePostgreSql = databaseConfig.GetValue("UsePostgreSql", false),
    PostgreSqlConnectionString = databaseConfig.GetValue("PostgreSqlConnectionString", string.Empty),
    UseRedis = cacheConfig.GetValue("UseRedis", false),
    RedisConnectionString = cacheConfig.GetValue("RedisConnectionString", string.Empty),
    IdempotencyExpiration = TimeSpan.FromHours(idempotencyConfig.GetValue("ExpirationHours", 24)),
    ExpirationCheckInterval = TimeSpan.FromMinutes(idempotencyConfig.GetValue("CheckIntervalMinutes", 1)),
    TenantsBillingBaseUrl = servicesConfig["TenantsBillingBaseUrl"]
        ?? throw new InvalidOperationException("Configuration 'Services:TenantsBillingBaseUrl' is required.")
};

builder.Services.AddServiceTokens(builder.Configuration);
builder.Services.AddAlertOrchestratorInfrastructure(infrastructureOptions);

var readiness = builder.Services.AddHealthChecks();
if (infrastructureOptions.UsePostgreSql)
    readiness.AddNpgSql(infrastructureOptions.PostgreSqlConnectionString, name: "postgresql",
        tags: [ObservabilityExtensions.ReadinessTag], timeout: TimeSpan.FromSeconds(3));
if (infrastructureOptions.UseRedis)
    readiness.AddRedis(infrastructureOptions.RedisConnectionString, name: "redis",
        tags: [ObservabilityExtensions.ReadinessTag], timeout: TimeSpan.FromSeconds(3));

var app = builder.Build();

app.UseSentinelRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Sentinel - Alert Orchestrator API")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    }).AllowAnonymous();
}

app.UseExceptionHandler();
app.UseWhen(ApiProblems.IsVersionedApi, versioned => versioned.UseStatusCodePages());
app.UseHttpsRedirection();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapSentinelOperationalEndpoints();

app.Run();

public partial class Program;