using System.Collections.Concurrent;
using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Infrastructure.Messaging.Reliability;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace ChannelsService.Infrastructure.Services;

/// <summary>
/// Retry → circuit breaker → per-attempt timeout. Only transient failures are retried, because sending is not
/// idempotent at the provider (a retried SMS may be delivered twice). Circuit breakers are kept per provider for the
/// lifetime of the process, so the engine must be registered as a singleton.
/// </summary>
public sealed class RetryPolicyEngine : IRetryPolicyEngine
{
    /// <summary>Platform limit: at most 3 attempts per operation, including the first.</summary>
    public const int MaxAttemptsLimit = 3;

    private readonly ConcurrentDictionary<string, AsyncCircuitBreakerPolicy<DeliveryResultDTO>> _breakers = new();
    private readonly ILogger<RetryPolicyEngine> _logger;
    private readonly Func<int, TimeSpan> _retryDelay;

    public RetryPolicyEngine(ILogger<RetryPolicyEngine> logger) : this(logger, null)
    {
    }

    public RetryPolicyEngine(ILogger<RetryPolicyEngine> logger, Func<int, TimeSpan>? retryDelay)
    {
        _logger = logger;
        _retryDelay = retryDelay ?? (retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) +
                                                     TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250)));
    }

    public async Task<DeliveryResultDTO> ExecuteAsync(string providerName,
        Func<CancellationToken, Task<DeliveryResultDTO>> sendFunc, int maxAttempts,
        ProviderResilienceDTO resilienceDto, CancellationToken cancellationToken)
    {
        var attempts = Math.Clamp(maxAttempts, 1, MaxAttemptsLimit);

        var retry = Policy<DeliveryResultDTO>
            .Handle<Exception>(TransientFailureClassifier.IsTransient)
            .OrResult(result => !result.Success && result.IsTransient)
            .WaitAndRetryAsync(attempts - 1, _retryDelay, (outcome, delay, retryCount, _) =>
            {
                if (outcome.Exception is not null)
                    _logger.LogWarning(outcome.Exception, "{Provider} attempt {Attempt} failed; retrying in {DelayMs} ms",
                        providerName, retryCount, (int)delay.TotalMilliseconds);
                else
                    _logger.LogWarning("{Provider} attempt {Attempt} returned {Error}; retrying in {DelayMs} ms",
                        providerName, retryCount, outcome.Result?.Error, (int)delay.TotalMilliseconds);
            });

        IAsyncPolicy<DeliveryResultDTO> perAttempt = Policy.NoOpAsync<DeliveryResultDTO>();
        if (resilienceDto.TimeoutSeconds > 0)
            perAttempt = Policy.TimeoutAsync<DeliveryResultDTO>(TimeSpan.FromSeconds(resilienceDto.TimeoutSeconds),
                TimeoutStrategy.Optimistic);
        if (resilienceDto.CircuitBreakerFailureThreshold > 0)
            perAttempt = Breaker(providerName, resilienceDto).WrapAsync(perAttempt);

        try
        {
            return await retry.WrapAsync(perAttempt).ExecuteAsync(sendFunc, cancellationToken);
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("Circuit for {Provider} is open; skipping the provider", providerName);
            return Failure(providerName, "The provider is temporarily unavailable (circuit open).");
        }
        catch (TimeoutRejectedException)
        {
            return Failure(providerName, "The provider did not answer in time.");
        }
    }

    private AsyncCircuitBreakerPolicy<DeliveryResultDTO> Breaker(string providerName, ProviderResilienceDTO options) =>
        _breakers.GetOrAdd(providerName, _ => Policy<DeliveryResultDTO>
            .Handle<Exception>(TransientFailureClassifier.IsTransient)
            .OrResult(result => !result.Success && result.IsTransient)
            .CircuitBreakerAsync(
                options.CircuitBreakerFailureThreshold,
                TimeSpan.FromSeconds(options.CircuitBreakerDurationSeconds),
                (outcome, breakDelay) => _logger.LogWarning(outcome.Exception,
                    "Circuit for {Provider} opened for {BreakDelay}", providerName, breakDelay),
                () => _logger.LogInformation("Circuit for {Provider} reset", providerName),
                () => _logger.LogInformation("Circuit for {Provider} is half-open", providerName)));

    private static DeliveryResultDTO Failure(string providerName, string error) =>
        new() { Success = false, IsTransient = true, Error = error, ProviderName = providerName };
}
