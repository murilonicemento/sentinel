using ChannelsService.Application.DTOs;
using ChannelsService.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChannelsService.UnitTests;

public sealed class RetryPolicyEngineTests
{
    private static readonly ProviderResilienceDTO NoBreaker = new() { CircuitBreakerFailureThreshold = 0 };

    private static RetryPolicyEngine Engine() =>
        new(NullLogger<RetryPolicyEngine>.Instance, _ => TimeSpan.Zero);

    private static Func<CancellationToken, Task<DeliveryResultDTO>> Counting(Func<int, DeliveryResultDTO> result,
        Action onCall) => _ =>
    {
        onCall();
        return Task.FromResult(result(0));
    };

    [Fact]
    public async Task PermanentFailure_IsNotRetried()
    {
        var calls = 0;

        var result = await Engine().ExecuteAsync("sms", Counting(_ => new DeliveryResultDTO
        {
            Success = false,
            IsTransient = false,
            Error = "Recipient phone number is missing."
        }, () => calls++), 3, NoBreaker, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task TransientFailure_IsRetriedUpToThePlatformLimitOfThreeAttempts()
    {
        var calls = 0;

        await Engine().ExecuteAsync("sms", Counting(_ => new DeliveryResultDTO
        {
            Success = false,
            IsTransient = true,
            Error = "503"
        }, () => calls++), maxAttempts: 10, NoBreaker, CancellationToken.None);

        Assert.Equal(RetryPolicyEngine.MaxAttemptsLimit, calls);
    }

    [Fact]
    public async Task MaxAttempts_IsTheTotalNumberOfAttempts()
    {
        var calls = 0;

        await Engine().ExecuteAsync("sms", Counting(_ => new DeliveryResultDTO
        {
            Success = false,
            IsTransient = true
        }, () => calls++), maxAttempts: 2, NoBreaker, CancellationToken.None);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task NonTransientException_PropagatesWithoutRetry()
    {
        var calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Engine().ExecuteAsync("sms", _ =>
        {
            calls++;
            throw new InvalidOperationException("bug");
        }, 3, NoBreaker, CancellationToken.None));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task OpenCircuit_SkipsTheProviderAndReportsATransientFailure()
    {
        var engine = Engine();
        var options = new ProviderResilienceDTO { CircuitBreakerFailureThreshold = 2, CircuitBreakerDurationSeconds = 60 };
        var calls = 0;
        var failing = Counting(_ => new DeliveryResultDTO { Success = false, IsTransient = true }, () => calls++);

        await engine.ExecuteAsync("sms", failing, 1, options, CancellationToken.None);
        await engine.ExecuteAsync("sms", failing, 1, options, CancellationToken.None);
        var skipped = await engine.ExecuteAsync("sms", failing, 1, options, CancellationToken.None);
        var otherProvider = await engine.ExecuteAsync("email", Counting(_ => new DeliveryResultDTO { Success = true },
            () => { }), 1, options, CancellationToken.None);

        Assert.Equal(2, calls);
        Assert.False(skipped.Success);
        Assert.True(skipped.IsTransient);
        Assert.True(otherProvider.Success);
    }
}
