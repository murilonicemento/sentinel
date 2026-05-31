using System.Net;
using System.Text.Json.Serialization;
using ChannelsService.Application;
using ChannelsService.Infrastructure;
using ChannelsService.Infrastructure.Messaging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using HealthChecks.UI.Client;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
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

builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .WriteTo.Console();
});

builder.Services.AddApplicationServiceCollection();
builder.Services.AddChannelsServiceInfrastructure(builder.Configuration);

var healthChecks = builder.Services.AddHealthChecks();

var dbConnection = builder.Configuration.GetConnectionString("ChannelsServiceDatabase");
if (!string.IsNullOrWhiteSpace(dbConnection))
{
    healthChecks.AddNpgSql(dbConnection, name: "postgresql", tags: new[] { "db" });
}

var redisConnection = builder.Configuration["Redis:Configuration"];
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    healthChecks.AddRedis(redisConnection, name: "redis", tags: new[] { "cache" });
}

healthChecks.AddCheck<KafkaHealthCheck>("kafka", tags: new[] { "kafka", "messaging" });

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("ChannelsService"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Sentinel - Channels Service API")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();
