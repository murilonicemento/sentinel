namespace AuditCompliance.Application.Common;

public enum ErrorTypeEnum
{
    Validation,
    NotFound,
    Conflict,
    Gone,
    Unprocessable
}

public sealed record Error(
    string Code,
    string Message,
    ErrorTypeEnum Type,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null);

public static class ApplicationErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string ResourceNotFound = "RESOURCE_NOT_FOUND";
    public const string ExportNotReady = "EXPORT_NOT_READY";
    public const string ExportFailed = "EXPORT_FAILED";
    public const string ExportExpired = "EXPORT_EXPIRED";
    public const string ExportLimitExceeded = "EXPORT_LIMIT_EXCEEDED";
    public const string ExportProcessingAbandoned = "EXPORT_PROCESSING_ABANDONED";
    public const string MalformedMessage = "MALFORMED_MESSAGE";
    public const string MissingRequiredField = "MISSING_REQUIRED_FIELD";
    public const string UnexpectedProcessingError = "UNEXPECTED_PROCESSING_ERROR";
}
