namespace Ingestion.Application.Errors;

public enum IngestionErrorKind
{
    Conflict,
    Unprocessable
}

/// <summary>Expected business failure with a stable error code, mapped by /api/v1 to 409 or 422.</summary>
public interface IIngestionError
{
    IngestionErrorKind Kind { get; }

    string Code { get; }
}

/// <summary>
/// A resource referenced by the request does not exist. Derives from <see cref="KeyNotFoundException"/> so the legacy
/// routes keep answering 400.
/// </summary>
public sealed class IngestionReferenceNotFoundException(string code, string message)
    : KeyNotFoundException(message), IIngestionError
{
    public IngestionErrorKind Kind => IngestionErrorKind.Unprocessable;

    public string Code { get; } = code;
}

/// <summary>
/// The request conflicts with the current state. Derives from <see cref="InvalidOperationException"/> to keep the
/// legacy routes' behavior unchanged.
/// </summary>
public sealed class IngestionRuleViolationException(IngestionErrorKind kind, string code, string message)
    : InvalidOperationException(message), IIngestionError
{
    public IngestionErrorKind Kind { get; } = kind;

    public string Code { get; } = code;
}

public static class IngestionErrorCodes
{
    public const string TenantNotFound = "TENANT_NOT_FOUND";
    public const string TenantInactive = "TENANT_INACTIVE";
    public const string DataSourceNotFound = "DATA_SOURCE_NOT_FOUND";
    public const string DataSourceAlreadyExists = "DATA_SOURCE_ALREADY_EXISTS";
}
