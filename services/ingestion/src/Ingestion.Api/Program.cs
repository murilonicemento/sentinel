using Ingestion.Api.Middlewares;
using Ingestion.Application;
using Ingestion.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services
    .AddOpenApi()
    .AddApplicationServiceCollection()
    .AddInfrastructureServiceCollection()
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
                statusCode = 400,
                errors = new
                {
                    messages = errors
                }
            };

            return new BadRequestObjectResult(responseObj);
        };
    });
;

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