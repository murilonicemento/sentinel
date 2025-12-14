namespace Ingestion.Domain.ValueObjects;

public class CollectionFrequencyType : ValueObject
{
    public string Value { get; }
    private static readonly CollectionFrequencyType Hourly = new("Hourly");
    private static readonly CollectionFrequencyType Daily = new("Daily");
    private static readonly CollectionFrequencyType Weekly = new("Weekly");

    private CollectionFrequencyType(string value)
    {
        Value = value;
    }

    public static CollectionFrequencyType From(string value)
    {
        return value switch
        {
            "Hourly" => Hourly,
            "Daily" => Daily,
            "Weekly" => Weekly,
            _ => throw new ArgumentException($"Invalid collection frequency type: {value}")
        };
    }

    public bool IsValidFrequency(DateTime lastDataCollectedDateTime)
    {
        return Value switch
        {
            "Hourly" => (DateTime.Now - lastDataCollectedDateTime).TotalHours > 1,
            "Daily" => (DateTime.Now - lastDataCollectedDateTime).TotalDays > 1,
            "Weekly" => (DateTime.Now - lastDataCollectedDateTime).TotalDays > 7,
            _ => false
        };
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}