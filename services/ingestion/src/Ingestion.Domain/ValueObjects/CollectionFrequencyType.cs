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

    /// <summary>
    /// Compares instants in UTC: Postgres returns <c>collected_at</c> as UTC, and comparing it with local time shifted
    /// the window by the server offset. An <c>Unspecified</c> value is taken as UTC, like in <c>NotInFuture</c>.
    /// </summary>
    public bool IsValidFrequency(DateTime lastDataCollectedDateTime)
    {
        var elapsed = DateTime.UtcNow - lastDataCollectedDateTime.Kind switch
        {
            DateTimeKind.Local => lastDataCollectedDateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(lastDataCollectedDateTime, DateTimeKind.Utc)
        };
        return Value switch
        {
            "Hourly" => elapsed.TotalHours > 1,
            "Daily" => elapsed.TotalDays > 1,
            "Weekly" => elapsed.TotalDays > 7,
            _ => false
        };
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}