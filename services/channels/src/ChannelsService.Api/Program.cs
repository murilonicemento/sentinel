using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json.Serialization;
using ChannelsService.Api.Contracts;
using ChannelsService.Api.Observability;
using ChannelsService.Api.Security;
using ChannelsService.Application;
using ChannelsService.Infrastructure;
using ChannelsService.Infrastructure.Messaging;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
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
builder.Services.AddChannelsServiceAuthentication(builder.Configuration);

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

builder.AddSentinelObservability("channels", activitySources: ["ChannelsService"], meters: ["ChannelsService"]);

builder.Services.AddApplicationServiceCollection();
builder.Services.AddChannelsServiceInfrastructure(builder.Configuration);

var healthChecks = builder.Services.AddHealthChecks();
var dbConnection = builder.Configuration.GetConnectionString("ChannelsServiceDatabase");

if (!string.IsNullOrWhiteSpace(dbConnection))
{
    healthChecks.AddNpgSql(dbConnection, name: "postgresql", tags: [ObservabilityExtensions.ReadinessTag],
        timeout: TimeSpan.FromSeconds(3));
}

var redisConnection = builder.Configuration["Redis:Configuration"];

if (!string.IsNullOrWhiteSpace(redisConnection))
{
    healthChecks.AddRedis(redisConnection, name: "redis", tags: [ObservabilityExtensions.ReadinessTag],
        timeout: TimeSpan.FromSeconds(3));
}

// Kafka feeds the consumer, not the HTTP API, so it is reported on /health but does not gate readiness.
healthChecks.AddCheck<KafkaHealthCheck>("kafka", tags: ["kafka", "messaging"]);

var app = builder.Build();

app.UseSentinelRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Sentinel - Channels Service API")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    }).AllowAnonymous();
}

app.UseExceptionHandler();
app.UseWhen(ApiProblems.IsVersionedApi, versioned => versioned.UseStatusCodePages());
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapSentinelOperationalEndpoints();

app.Run();

public partial class Program;
