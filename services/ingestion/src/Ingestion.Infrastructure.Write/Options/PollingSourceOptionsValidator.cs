using Microsoft.Extensions.Options;

namespace Ingestion.Infrastructure.Write.Options;

/// <summary>Fails startup for an enabled source with every invalid key listed (named options, one per source).</summary>
public sealed class PollingSourceOptionsValidator : IValidateOptions<PollingSourceOptions>
{
    public ValidateOptionsResult Validate(string? name, PollingSourceOptions options)
    {
        if (name is null || !PollingSources.All.Contains(name)) return ValidateOptionsResult.Skip;
        var errors = PollingSources.Validate(name, options).ToList();
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
