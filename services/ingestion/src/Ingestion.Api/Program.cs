using System.Net;
using System.Text;
using Ingestion.Api.Filters;
using Ingestion.Api.Middlewares;
using Ingestion.Application;
using Ingestion.Infrastructure.Read;
using Ingestion.Infrastructure.Write;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Sinks.Elasticsearch;

namespace Ingestion.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        var pack = new ConventionPack
        {
            new IgnoreExtraElementsConvention(true)
        };
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        ConventionRegistry.Register("IgnoreExtra", pack, _ => true);

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId()
            .WriteTo.Console()
            .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(builder.Configuration["ElasticSearch:URI"]!))
            {
                AutoRegisterTemplate = true,
                IndexFormat = $"ingestion-logs-{environment.ToLower()}-{DateTime.UtcNow:yyyy-MM}",
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

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("IngestionRead", policy =>
                policy.RequireRole("Ingestion.Admin", "Ingestion.Read"))
            .AddPolicy("IngestionWrite", policy =>
                policy.RequireRole("Ingestion.Admin", "Ingestion.Write"));

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                options
                    .WithTitle("Sentinel - Ingestion API")
                    .WithTheme(ScalarTheme.DeepSpace)
                    .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                    .AddPreferredSecuritySchemes("BearerAuth")
                    .AddHttpAuthentication("BearerAuth", auth => { auth.Token = builder.Configuration["Scalar:AuthToken"]; });
            });
        }

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.MapControllers();
        app.Run();
    }
}