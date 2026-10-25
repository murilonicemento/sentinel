namespace TenantsBilling.Application.Errors;

public enum TenantsBillingErrorKind
{
    NotFound,
    Conflict,
    Unprocessable
}

/// <summary>Expected business failure with a stable, machine-readable error code.</summary>
public sealed class TenantsBillingException : Exception
{
    public TenantsBillingException(TenantsBillingErrorKind kind, string code, string message) : base(message)
    {
        Kind = kind;
        Code = code;
    }

    public TenantsBillingErrorKind Kind { get; }

    public string Code { get; }
}

public static class TenantsBillingErrorCodes
{
    public const string TenantNotFound = "TENANT_NOT_FOUND";
    public const string TenantAlreadyExists = "TENANT_ALREADY_EXISTS";
    public const string PlanNotFound = "PLAN_NOT_FOUND";
}
