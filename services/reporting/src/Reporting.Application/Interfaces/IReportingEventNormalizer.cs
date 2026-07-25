using Reporting.Application.Models;
using Reporting.Domain.Entities;

namespace Reporting.Application.Interfaces;

public interface IReportingEventNormalizer
{
    ReportingEventNormalizationResult Normalize(ReportingEventEnvelope envelope);
}
