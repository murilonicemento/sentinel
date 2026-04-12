using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Models;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Application.Services;

public sealed class RetryPolicyEngine : IRetryPolicyEngine
{
    private readonly ILogger<RetryPolicyEngine> _logger;

    public RetryPolicyEngine(ILogger<RetryPolicyEngine> logger)
    {
        _logger = logger;
    }

    public async Task<DeliveryResult> ExecuteAsync(Func<Task<DeliveryResult>> sendFunc, int maxAttempts, CancellationToken cancellationToken)
    {
        DeliveryResult lastResult = new() { Success = false, Error = "No send attempt executed." };

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new DeliveryResult { Success = false, Error = "Retry cancelled." };
            }

            try
            {
                lastResult = await sendFunc();
                if (lastResult.Success)
                {
                    return lastResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error executing retry policy on attempt {Attempt}.", attempt);
                lastResult = new DeliveryResult { Success = false, Error = ex.Message };
            }

            if (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));
                await Task.Delay(delay, cancellationToken);
            }
        }

        return lastResult;
    }
}