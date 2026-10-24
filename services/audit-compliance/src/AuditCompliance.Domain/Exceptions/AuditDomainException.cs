namespace AuditCompliance.Domain.Exceptions;

public sealed class AuditDomainException : Exception
{
    public AuditDomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
