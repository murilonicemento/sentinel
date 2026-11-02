namespace Ingestion.Domain.Enums;

public static class ClimaticEventEnumExtension
{
    public static string GetDisplayName(this Enum value)
    {
        return value switch
        {
            ClimaticEventEnum.TemperatureAnomaly => "Temperature Anomaly",
            ClimaticEventEnum.HumidityAnomaly => "Humidity Anomaly",
            ClimaticEventEnum.WindGust => "Wind Gust",
            ClimaticEventEnum.Rainfall => "Rainfall",
            ClimaticEventEnum.PressureChange => "Pressure Change",
            _ => "Normal"
        };
    }
}