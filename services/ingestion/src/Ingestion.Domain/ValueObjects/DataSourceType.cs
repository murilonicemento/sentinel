namespace Ingestion.Domain.ValueObjects;

public class DataSourceType : ValueObject
{
    public string Value { get; }
    private static readonly DataSourceType Sensor = new("Sensor");
    private static readonly DataSourceType Api = new("Api");
    private static readonly DataSourceType File = new("File");
    private static readonly DataSourceType ExternalSystem = new("ExternalSystem");

    private DataSourceType(string value)
    {
        Value = value;
    }

    public static DataSourceType From(string value)
    {
        return value switch
        {
            "Sensor" => Sensor,
            "Api" => Api,
            "File" => File,
            "ExternalSystem" => ExternalSystem,
            _ => throw new ArgumentException($"Invalid DataSourceType: {value}")
        };
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}