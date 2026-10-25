using System.Net.Http.Headers;
using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ChannelsService.IntegrationTests.Support;

public sealed class ChannelsApiFactory : WebApplicationFactory<Program>
{
    public FakeDispatchService Dispatch { get; } = new();
    public InMemoryDeliveryRepository Attempts { get; } = new();
    public RecordingNotificationPublisher Publisher { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", TestTokens.Issuer);
        builder.UseSetting("Jwt:Audience", TestTokens.Audience);
        builder.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
        builder.UseSetting("ServiceToken:SigningKey", "integration-tests-channels-service-token-key-0123456789");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<INotificationDispatchService>();
            services.AddSingleton<INotificationDispatchService>(Dispatch);
            services.RemoveAll<IDeliveryRepository>();
            services.AddSingleton<IDeliveryRepository>(Attempts);
            services.RemoveAll<INotificationPublisher>();
            services.AddSingleton<INotificationPublisher>(Publisher);
        });
    }

    public HttpClient CreateClientWithToken(string? token)
    {
        var client = CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

public sealed class FakeDispatchService : INotificationDispatchService
{
    public bool Succeeds { get; set; } = true;

    public Task<DeliveryResultDTO> DispatchAsync(NotificationEvent notification, CancellationToken cancellationToken) =>
        Task.FromResult(new DeliveryResultDTO
        {
            Success = Succeeds,
            ProviderName = Succeeds ? "sms-provider" : null,
            Error = Succeeds ? null : "provider timeout at 10.0.0.12"
        });
}

public sealed class InMemoryDeliveryRepository : IDeliveryRepository
{
    private readonly List<DeliveryAttempt> _attempts = [];
    private readonly Lock _lock = new();

    public Task AddAsync(DeliveryAttempt attempt)
    {
        lock (_lock) _attempts.Add(attempt);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeliveryAttempt>> GetByEventAsync(string eventId)
    {
        lock (_lock)
            return Task.FromResult<IReadOnlyList<DeliveryAttempt>>(_attempts.Where(a => a.EventId == eventId).ToList());
    }
}

public sealed class RecordingNotificationPublisher : INotificationPublisher
{
    private readonly List<NotificationEvent> _published = [];

    public IReadOnlyList<NotificationEvent> Published => _published;

    public Task PublishAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        lock (_published) _published.Add(notification);
        return Task.CompletedTask;
    }
}
