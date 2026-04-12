using CsvHelper.Configuration.Attributes;

namespace Ingestion.Application.DTO;

public record FireFirmsApiResponseDTO
{
    [Index(0)] public double Latitude { get; set; }

    [Index(1)] public double Longitude { get; set; }

    [Index(2)] public double BrightTi4 { get; set; }

    [Index(3)] public double Scan { get; set; }

    [Index(4)] public double Track { get; set; }

    [Index(5)] public string AcqDate { get; set; } = string.Empty;

    [Index(6)] public string AcqTime { get; set; } = string.Empty;

    [Index(7)] public string Satellite { get; set; } = string.Empty;

    [Index(8)] public string Instrument { get; set; } = string.Empty;

    [Index(9)] public string Confidence { get; set; } = string.Empty;

    [Index(10)] public string Version { get; set; } = string.Empty;

    [Index(11)] public double BrightTi5 { get; set; }

    [Index(12)] public double Frp { get; set; }

    [Index(13)] public string DayNight { get; set; } = string.Empty;
}