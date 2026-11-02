using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using RiskCatalog.Api.Contracts;
using RiskCatalog.Api.Filters;
using RiskCatalog.Api.Middlewares;
using RiskCatalog.Api.Observability;
using RiskCatalog.Api.Security;
using RiskCatalog.Application;
using RiskCatalog.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddSentinelObservability("risk-catalog");
AddHealthChecks(builder);

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
    .AddInfrastructureServiceCollection(builder.Configuration)
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
builder.Services.AddRiskCatalogAuthentication(builder.Configuration);

var app = builder.Build();

app.UseSentinelRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Sentinel - Risk Catalog API")
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

// Postgres holds the catalog and gates readiness; Redis is only a read cache, reported on /health as degraded.
static void AddHealthChecks(WebApplicationBuilder builder)
{
    var timeout = TimeSpan.FromSeconds(3);
    var checks = builder.Services.AddHealthChecks();

    var database = builder.Configuration.GetConnectionString("RiskCatalogDatabase");
    if (!string.IsNullOrWhiteSpace(database))
        checks.AddNpgSql(database, name: "postgresql", tags: [ObservabilityExtensions.ReadinessTag], timeout: timeout);

    var redis = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrWhiteSpace(redis))
        checks.AddRedis(redis, name: "redis", failureStatus: HealthStatus.Degraded, timeout: timeout);
}

namespace RiskCatalog.Api
{
    public partial class Program
    {
    }
}