using System.Net;
using Ingestion.Api.Filters;
using Ingestion.Api.Middlewares;
using Ingestion.Application;
using Ingestion.Infrastructure.Read;
using Ingestion.Infrastructure.Write;
using Microsoft.AspNetCore.Mvc;
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
            .AddOpenApi()
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

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.UseHttpsRedirection();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.MapControllers();
        app.Run();
    }
}