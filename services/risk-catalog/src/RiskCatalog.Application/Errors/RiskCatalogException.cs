namespace RiskCatalog.Application.Errors;

public enum RiskCatalogErrorKind
{
    Conflict,
    Unprocessable
}

/// <summary>
/// Expected business failure with a stable error code. It derives from <see cref="ArgumentException"/> so the legacy
/// routes keep answering 400 with the same message, while /api/v1 maps it to 409 or 422.
/// </summary>
public sealed class RiskCatalogException : ArgumentException
{
    public RiskCatalogException(RiskCatalogErrorKind kind, string code, string message) : base(message)
    {
        Kind = kind;
        Code = code;
    }

    public RiskCatalogErrorKind Kind { get; }

    public string Code { get; }
}

public static class RiskCatalogErrorCodes
{
    public const string EventTypeAlreadyExists = "EVENT_TYPE_ALREADY_EXISTS";
    public const string EventTypeNotFound = "EVENT_TYPE_NOT_FOUND";
    public const string SeverityAlreadyExists = "SEVERITY_ALREADY_EXISTS";
    public const string SeverityNotFound = "SEVERITY_NOT_FOUND";
    public const string SeverityCriterionOverlap = "SEVERITY_CRITERION_OVERLAP";
    public const string RiskMatrixAlreadyExists = "RISK_MATRIX_ALREADY_EXISTS";
    public const string IdfCurveAlreadyExists = "IDF_CURVE_ALREADY_EXISTS";
    public const string RegionalParametersAlreadyExist = "REGIONAL_PARAMETERS_ALREADY_EXIST";
    public const string CatalogVersionNotFound = "CATALOG_VERSION_NOT_FOUND";
}
