namespace RiskCatalog.Domain.EventTypes;

public class EventType
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }

    public EventType()
    {
    }

    public EventType(Guid id, string code, string name, string description, bool isActive)
    {
        Id = id;
        Code = code;
        Name = name;
        Description = description;
        IsActive = isActive;
    }
}