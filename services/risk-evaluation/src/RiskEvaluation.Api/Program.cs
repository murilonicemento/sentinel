using RiskEvaluation.Api;
using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Application.Services;
using RiskEvaluation.Domain.Services;
using RiskEvaluation.Infrastructure.Messaging;
using RiskEvaluation.Infrastructure.Persistence;
using MediatR;
using MongoDB.Driver;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Logging
builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration);
});

// MongoDB
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("MongoDb");
    return new MongoClient(connectionString);
});

builder.Services.AddScoped(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase("RiskEvaluationDb");
});

// Repositories
builder.Services.AddScoped<IRiskEvaluationRepository, RiskEvaluationRepository>();

// Domain Services
builder.Services.AddScoped<RiskCalculationService>();

// Application Services
builder.Services.AddScoped<IRiskEvaluationService, RiskEvaluationService>();

// Messaging
builder.Services.AddSingleton<IEventPublisher>(sp =>
{
    var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"];
    var topic = builder.Configuration["Kafka:Topic"];
    return new KafkaEventPublisher(bootstrapServers!, topic!);
});

builder.Services.AddSingleton<IMessageConsumer>(sp =>
{
    var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"];
    var groupId = builder.Configuration["Kafka:GroupId"];
    var mediator = sp.GetRequiredService<IMediator>();
    return new KafkaMessageConsumer(bootstrapServers!, groupId!, mediator);
});

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Hosted Service for Consumer
builder.Services.AddHostedService<MessageConsumerHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();