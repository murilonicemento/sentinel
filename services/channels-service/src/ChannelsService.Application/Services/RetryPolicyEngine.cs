using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;

namespace ChannelsService.Application.Services;

public sealed class RetryPolicyEngine : IRetryPolicyEngine
{
    private readonly ILogger<RetryPolicyEngine> _logger;

    public RetryPolicyEngine(ILogger<RetryPolicyEngine> logger)
    {
        _logger = logger;
    }

    public async Task<DeliveryResultDTO> ExecuteAsync(Func<CancellationToken, Task<DeliveryResultDTO>> sendFunc, int maxAttempts, ProviderResilienceDTO resilienceDto, CancellationToken cancellationToken)
    {
        var jitter = new Random();

        var retryPolicy = Policy<DeliveryResultDTO>
            .Handle<Exception>()
            .OrResult(result => !result.Success)
            .WaitAndRetryAsync(
                maxAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(jitter.Next(0, 250)),
                onRetry: (outcome, timeSpan, retryCount, context) =>
                {
                    if (outcome.Exception != null)
                    {
                        _logger.LogWarning(outcome.Exception, "Retry {RetryCount} failed with exception.", retryCount);
                    }
                    else
                    {
                        _logger.LogWarning("Retry {RetryCount} received unsuccessful result: {Error}", retryCount, outcome.Result?.Error);
                    }
                });

        AsyncPolicy<DeliveryResultDTO> policy = retryPolicy;

        if (resilienceDto.CircuitBreakerFailureThreshold > 0)
        {
            var circuitBreakerPolicy = Policy<DeliveryResultDTO>
                .Handle<Exception>()
                .OrResult(result => !result.Success)
                .CircuitBreakerAsync(
                    resilienceDto.CircuitBreakerFailureThreshold,
                    TimeSpan.FromSeconds(resilienceDto.CircuitBreakerDurationSeconds),
                    onBreak: (outcome, breakDelay, context) =>
                    {
                        _logger.LogWarning(outcome.Exception, "Circuit breaker opened for {BreakDelay} after failure.", breakDelay);
                    },
                    onReset: context => _logger.LogInformation("Circuit breaker reset."),
                    onHalfOpen: () => _logger.LogInformation("Circuit breaker is half-open."));

            policy = circuitBreakerPolicy.WrapAsync(policy);
        }

        if (resilienceDto.TimeoutSeconds > 0)
        {
            var timeoutPolicy = Policy.TimeoutAsync<DeliveryResultDTO>(TimeSpan.FromSeconds(resilienceDto.TimeoutSeconds), TimeoutStrategy.Optimistic);
            policy = timeoutPolicy.WrapAsync(policy);
        }

        var executionResult = await policy.ExecuteAsync(async ct => await sendFunc(ct), cancellationToken);
        return executionResult;
    }
}