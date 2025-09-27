namespace Ingestion.Domain.ValueObjects;

public class MeasurementType : ValueObject
{
    public string Value { get; }
    private static readonly MeasurementType Temperature = new("Temperature");
    private static readonly MeasurementType Humidity = new("Humidity");
    private static readonly MeasurementType WindSpeed = new("WindSpeed");
    private static readonly MeasurementType Rainfall = new("Rainfall");
    private static readonly MeasurementType Pressure = new("Pressure");

    private MeasurementType(string value)
    {
        Value = value;
    }

    public static MeasurementType From(string value)
    {
        return value switch
        {
            "Temperature" => Temperature,
            "Humidity" => Humidity,
            "WindSpeed" => WindSpeed,
            "Rainfall" => Rainfall,
            "Pressure" => Pressure,
            _ => throw new ArgumentException($"Invalid measurement type: {value}")
        };
    }

    public bool IsValidUnit(string unit)
    {
        var measurementUnits = new Dictionary<string, string[]>
        {
            {
                "Temperature", ["C", "F", "K"]
            },
            {
                "WindSpeed", ["km/h", "m/s"]
            },
            {
                "Pressure", ["hPa", "atm"]
            }
        };

        return Value switch
        {
            "Temperature" or "WindSpeed" or "Pressure" => measurementUnits[Value].Contains(unit),
            "Humidity" => unit == "%",
            "Rainfall" => unit == "mm",
            _ => false
        };
    }

    public double CalculateIntensity(double intensityValue, double baseline = 0)
    {
        return Value switch
        {
            "Temperature" or "Humidity" => intensityValue - baseline,
            "WindSpeed" or "Rainfall" => intensityValue,
            "Pressure" => baseline > 0 ? intensityValue - baseline : intensityValue - 1013,
            _ => throw new ArgumentException($"Invalid measurement type: {intensityValue}")
        };
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}