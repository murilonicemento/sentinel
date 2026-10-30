using System.Net;
using Ingestion.Api.Contracts;
using Ingestion.Api.Filters;
using Ingestion.Api.Middlewares;
using Ingestion.Api.Observability;
using Ingestion.Api.Security;
using Ingestion.Application;
using Ingestion.Infrastructure.Read;
using Ingestion.Infrastructure.Read.HealthChecks;
using Ingestion.Infrastructure.Write;
using Ingestion.Infrastructure.Write.HostedServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using Scalar.AspNetCore;

namespace Ingestion.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        AddPollingSubscriptionsFile(builder);

        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        var pack = new ConventionPack
        {
            new IgnoreExtraElementsConvention(true)
        };
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        ConventionRegistry.Register("IgnoreExtra", pack, _ => true);

        builder.AddSentinelObservability("ingestion", meters: [PollingMetrics.MeterName, OutboxMetrics.MeterName]);
        AddReadinessChecks(builder);

        builder.Services.AddControllers(options => { options.Filters.Add<ResponseWrapperFilter>(); });
        builder.Services
            .AddOpenApi(options =>
            {
                options.MarkDeprecatedOperations();
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes.Add("BearerAuth", new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT"
                    });
                    return Task.CompletedTask;
                });

                options.AddOperationTransformer((operation, context, cancellationToken) =>
                {
                    var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                    var hasAuthorize = metadata.Any(m => m is IAuthorizeData);
                    var hasAllowAnonymous = metadata.Any(m => m is IAllowAnonymous);

                    if (hasAuthorize && !hasAllowAnonymous)
                    {
                        operation.Security = new List<OpenApiSecurityRequirement>
                        {
                            new OpenApiSecurityRequirement
                            {
                                {
                                    new OpenApiSecurityScheme
                                    {
                                        Reference = new OpenApiReference
                                        {
                                            Type = ReferenceType.SecurityScheme,
                                            Id = "BearerAuth"
                                        }
                                    },
                                    Array.Empty<string>()
                                }
                            }
                        };
                    }

                    return Task.CompletedTask;
                });
            })
            .AddApplicationServiceCollection()
            .AddInfrastructureReadServiceCollection()
            .AddInfrastructureWriteServiceCollection(builder.Configuration)
            .Configure<ApiBehaviorOptions>(options =>
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

        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var status = context.ProblemDetails.Status ?? StatusCodes.Status500InternalServerError;
                ApiProblems.Enrich(context.ProblemDetails, context.HttpContext, ApiProblems.DefaultCodeFor(status));
            };
        });
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddIngestionAuthentication(builder.Configuration);

        var app = builder.Build();

        app.UseSentinelRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference(options =>
            {
                options
                    .WithTitle("Sentinel - Ingestion API")
                    .WithTheme(ScalarTheme.DeepSpace)
                    .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                    .AddPreferredSecuritySchemes("BearerAuth")
                    .AddHttpAuthentication("BearerAuth", auth => { auth.Token = builder.Configuration["Scalar:AuthToken"]; });
            }).AllowAnonymous();
        }

        app.UseExceptionHandler();
        app.UseWhen(ApiProblems.IsVersionedApi, versioned => versioned.UseStatusCodePages());
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.MapControllers();
        app.MapSentinelOperationalEndpoints();
        app.Run();
    }

    /// <summary>
    /// Polling subscriptions (tenants, data sources, areas) come from an optional <c>appsettings.Polling.json</c>
    /// mounted by the deployment. It sits right after <c>appsettings.{Environment}.json</c>, so User Secrets and
    /// environment variables, where the provider keys live, still override it.
    /// </summary>
    private static void AddPollingSubscriptionsFile(WebApplicationBuilder builder)
    {
        var sources = builder.Configuration.Sources;
        var environmentFile = $"appsettings.{builder.Environment.EnvironmentName}.json";
        var anchor = sources.OfType<JsonConfigurationSource>().LastOrDefault(source =>
            source.Path is "appsettings.json" || source.Path == environmentFile);
        var file = new JsonConfigurationSource
        {
            Path = "appsettings.Polling.json",
            Optional = true,
            ReloadOnChange = false,
            FileProvider = builder.Environment.ContentRootFileProvider
        };
        sources.Insert(anchor is null ? 0 : sources.IndexOf(anchor) + 1, file);
    }

    /// <summary>Postgres (collections and outbox) and MongoDB (queries) gate readiness; Redis only deduplicates
    /// polled samples, so it is reported on /health without gating readiness.</summary>
    private static void AddReadinessChecks(WebApplicationBuilder builder)
    {
        var timeout = TimeSpan.FromSeconds(3);
        var checks = builder.Services.AddHealthChecks()
            .AddCheck<ReadDatabaseHealthCheck>("mongodb", tags: [ObservabilityExtensions.ReadinessTag],
                timeout: timeout);

        var writeDatabase = builder.Configuration.GetConnectionString("IngestionWriteDatabase");
        if (!string.IsNullOrWhiteSpace(writeDatabase))
            checks.AddNpgSql(writeDatabase, name: "postgresql", tags: [ObservabilityExtensions.ReadinessTag],
                timeout: timeout);

        var redis = builder.Configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redis))
            checks.AddRedis(redis, name: "redis", failureStatus: HealthStatus.Degraded, timeout: timeout);
    }
}