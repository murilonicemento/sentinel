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
        switch (Value)
        {
            case "Hourly":
                if (DateTime.Now.Day - lastDataCollectedDateTime.Day > 1)
                    return true;

                return DateTime.Now.Hour - lastDataCollectedDateTime.Hour > 1;
            case "Daily":
                return DateTime.Now.Day - lastDataCollectedDateTime.Day > 1;
            case "Weekly":
                return DateTime.Now.Day - lastDataCollectedDateTime.Day > 7;
            default:
                return false;
        }
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}