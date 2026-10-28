using ApiGateway.Api.Observability;
using ApiGateway.Api.RateLimiting;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddSentinelObservability("api-gateway");
builder.Services.AddOpenApi();
builder.Services.AddGatewayRateLimiting(builder.Configuration);
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
builder.Services.AddOcelot();

var app = builder.Build();

app.UseSentinelRequestLogging();
app.UseHttpsRedirection();
app.UseRateLimiter();

// Ocelot answers every request that reaches it, so the gateway's own endpoints (health, /metrics, OpenAPI) run first;
// the endpoint middleware passes unmatched requests on to Ocelot.
app.UseRouting();
app.MapSentinelOperationalEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseEndpoints(_ => { });

await app.UseOcelot();

app.Run();

public partial class Program;
