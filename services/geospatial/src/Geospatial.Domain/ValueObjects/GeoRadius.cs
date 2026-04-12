namespace Geospatial.Domain.ValueObjects;

public class GeoRadius
{
    public double Value { get; }
    public DistanceUnit Unit { get; }

    public GeoRadius(double value, DistanceUnit unit)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Radius must be greater than zero");

        Value = value;
        Unit = unit;
    }

    public double ToMeters()
    {
        return Unit switch
        {
            DistanceUnit.Meters => Value,
            DistanceUnit.Kilometers => Value * 1000,
            _ => throw new InvalidOperationException("Unsupported distance unit")
        };
    }
}