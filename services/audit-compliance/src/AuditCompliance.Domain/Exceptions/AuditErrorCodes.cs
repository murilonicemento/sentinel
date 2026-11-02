namespace AuditCompliance.Domain.Exceptions;

public static class AuditErrorCodes
{
    public const string InvalidEventId = "INVALID_EVENT_ID";
    public const string UnknownEventType = "UNKNOWN_EVENT_TYPE";
    public const string UnsupportedEventVersion = "UNSUPPORTED_EVENT_VERSION";
    public const string InvalidSourceService = "INVALID_SOURCE_SERVICE";
    public const string InvalidTimestamp = "INVALID_TIMESTAMP";
    public const string InvalidActor = "INVALID_ACTOR";
    public const string InvalidResource = "INVALID_RESOURCE";
    public const string InvalidField = "INVALID_FIELD";
    public const string PayloadLimitExceeded = "PAYLOAD_LIMIT_EXCEEDED";
    public const string IntegrityAlreadySealed = "INTEGRITY_ALREADY_SEALED";
    public const string InvalidExportState = "INVALID_EXPORT_STATE";
    public const string InvalidFilter = "INVALID_FILTER";
}
