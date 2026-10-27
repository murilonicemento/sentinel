namespace Geospatial.Infrastructure.Messaging.Reliability;

/// <summary>Outcome of a message handler: processed (including duplicates) or rejected for the dead-letter topic.</summary>
public sealed record MessageHandlingResult
{
    private MessageHandlingResult(string? errorCode, string? errorReason)
    {
        ErrorCode = errorCode;
        ErrorReason = errorReason;
    }

    public string? ErrorCode { get; }

    public string? ErrorReason { get; }

    public bool IsRejected => ErrorCode is not null;

    public static MessageHandlingResult Processed { get; } = new(null, null);

    /// <summary>The message can never be processed (invalid payload, unknown type...); it goes to the dead-letter topic.</summary>
    public static MessageHandlingResult Reject(string errorCode, string errorReason) => new(errorCode, errorReason);
}

public static class MessageErrorCodes
{
    public const string InvalidPayload = "INVALID_PAYLOAD";
    public const string UnexpectedProcessingError = "UNEXPECTED_PROCESSING_ERROR";
}
