using AuditCompliance.Domain.Exceptions;

namespace AuditCompliance.Domain.ValueObjects;

public sealed record ResourceReference
{
    public const int MaxLength = 128;

    private ResourceReference(string type, string id)
    {
        Type = type;
        Id = id;
    }

    public string Type { get; }
    public string Id { get; }

    public static ResourceReference Create(string? type, string? id)
    {
        if (string.IsNullOrWhiteSpace(type) || type.Length > MaxLength)
            throw new AuditDomainException(AuditErrorCodes.InvalidResource,
                $"Resource type is required and must have at most {MaxLength} characters.");

        if (string.IsNullOrWhiteSpace(id) || id.Length > MaxLength)
            throw new AuditDomainException(AuditErrorCodes.InvalidResource,
                $"Resource id is required and must have at most {MaxLength} characters.");

        return new ResourceReference(type.Trim(), id.Trim());
    }
}
