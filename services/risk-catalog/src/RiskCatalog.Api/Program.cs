using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using RiskCatalog.Api.Contracts;
using RiskCatalog.Api.Filters;
using RiskCatalog.Api.Middlewares;
using RiskCatalog.Api.Security;
using RiskCatalog.Application;
using RiskCatalog.Infrastructure;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Sinks.Elasticsearch;

var builder = WebApplication.CreateBuilder(args);

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId()
    .WriteTo.Console()
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(builder.Configuration["ElasticSearch:URI"]!))
    {
        AutoRegisterTemplate = true,
        IndexFormat = $"risk-catalog-logs-{environment.ToLower()}-{DateTime.UtcNow:yyyy-MM}",
        NumberOfShards = 1,
        NumberOfReplicas = 1,
        MinimumLogEventLevel = Serilog.Events.LogEventLevel.Information,
        FailureCallback = e =>
            Console.WriteLine("An error occurred while sending logs to Elasticsearch: " + e.MessageTemplate)
    })
    .CreateLogger();

builder.Host.UseSerilog();

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

app.Run();

namespace RiskCatalog.Api
{
    public partial class Program
    {
    }
}