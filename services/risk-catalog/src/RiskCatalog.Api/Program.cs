using System.Net;
using Microsoft.AspNetCore.Mvc;
using RiskCatalog.Api.Filters;
using RiskCatalog.Api.Middlewares;
using RiskCatalog.Application;
using RiskCatalog.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => { options.Filters.Add<ResponseWrapperFilter>(); });
builder.Services
    .AddOpenApi()
    .AddApplicationServiceCollection()
    .AddInfrastructureServiceCollection(builder.Configuration)
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