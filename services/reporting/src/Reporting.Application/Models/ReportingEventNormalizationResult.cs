using Reporting.Domain.Entities;

namespace Reporting.Application.Models;

public sealed class ReportingEventNormalizationResult
{
    public ReportingEventNormalizationResult(bool isValid, string[] errors, ReportingEventEnvelope? normalizedEnvelope)
    {
        IsValid = isValid;
        Errors = errors;
        NormalizedEnvelope = normalizedEnvelope;
    }

    public bool IsValid { get; }
    public IReadOnlyCollection<string> Errors { get; }
    public ReportingEventEnvelope? NormalizedEnvelope { get; }
}
