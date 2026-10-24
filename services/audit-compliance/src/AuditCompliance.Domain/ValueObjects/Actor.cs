using AuditCompliance.Domain.Enums;
using AuditCompliance.Domain.Exceptions;

namespace AuditCompliance.Domain.ValueObjects;

public sealed record Actor
{
    public const int MaxIdLength = 128;

    private Actor(ActorTypeEnum type, string id)
    {
        Type = type;
        Id = id;
    }

    public ActorTypeEnum Type { get; }
    public string Id { get; }

    public static Actor Create(ActorTypeEnum type, string? id)
    {
        if (!Enum.IsDefined(type))
            throw new AuditDomainException(AuditErrorCodes.InvalidActor, "Actor type is not supported.");

        if (string.IsNullOrWhiteSpace(id) || id.Length > MaxIdLength)
            throw new AuditDomainException(AuditErrorCodes.InvalidActor,
                $"Actor id is required and must have at most {MaxIdLength} characters.");

        return new Actor(type, id.Trim());
    }
}
