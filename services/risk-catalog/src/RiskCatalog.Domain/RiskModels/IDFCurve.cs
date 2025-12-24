namespace RiskCatalog.Domain.RiskModels;

public class IDFCurve
{
    public Guid Id { get; set; }
    public Guid EventTypeId { get; set; }
    public int DurationMinutes { get; set; }
    public double Intensity { get; set; }
    public int ReturnPeriodYears { get; set; }
    public int Version { get; set; }

    public IDFCurve()
    {
    }

    public IDFCurve(
        Guid id,
        Guid eventTypeId,
        int durationMinutes,
        double intensity,
        int returnPeriodYears,
        int version
    )
    {
        Id = id;
        EventTypeId = eventTypeId;
        DurationMinutes = durationMinutes;
        Intensity = intensity;
        ReturnPeriodYears = returnPeriodYears;
        Version = version;
    }
}