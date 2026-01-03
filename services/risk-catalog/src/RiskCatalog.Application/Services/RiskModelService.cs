using Microsoft.Extensions.Logging;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Interfaces;
using RiskCatalog.Application.Services.Interfaces;
using RiskCatalog.Domain.Enums;
using RiskCatalog.Domain.IRepositories;
using RiskCatalog.Domain.RiskModels;

namespace RiskCatalog.Application.Services;

public class RiskModelService : IRiskModelService
{
    private readonly IEventTypeRepository _eventTypeRepository;
    private readonly IRiskMatrixRepository _riskMatrixRepository;
    private readonly IIDFCurveRepository _IDFCurveRepository;
    private readonly IRegionalParameterRepository _regionalParameterRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<RiskModelService> _logger;
    private const string RiskMatrixCachePrefix = "risk-matrix";
    private const string IDFCurveCachePrefix = "idf-curve";
    private const string RegionalParameterCachePrefix = "regional-parameter";

    public RiskModelService(
        IEventTypeRepository eventTypeRepository,
        IRiskMatrixRepository riskMatrixRepository,
        IIDFCurveRepository idfCurveRepository,
        IRegionalParameterRepository regionalParameterRepository,
        ICacheService cacheService,
        ILogger<RiskModelService> logger)
    {
        _eventTypeRepository = eventTypeRepository;
        _riskMatrixRepository = riskMatrixRepository;
        _IDFCurveRepository = idfCurveRepository;
        _regionalParameterRepository = regionalParameterRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<RiskMatrixDTO?> GetRiskMatrixForEventTypeAsync(
        string eventTypeCode,
        string severityLevel,
        int? version,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{RiskMatrixCachePrefix}:{eventTypeCode}:{severityLevel}:{version ?? 0}";

        _logger.LogInformation(
            "Retrieving risk matrix for event type. EventTypeCode: {eventTypeCode}, SeverityLevel: {severityLevel}, Version: {version}",
            eventTypeCode,
            severityLevel,
            version);

        var cachedResult = await _cacheService.GetAsync<RiskMatrixDTO>(cacheKey, cancellationToken);
        if (cachedResult != null)
        {
            _logger.LogInformation("Risk matrix retrieved from cache. EventTypeCode: {eventTypeCode}", eventTypeCode);
            return cachedResult;
        }

        var riskMatrix = await _riskMatrixRepository.GetRiskMatrixForEventTypeAsync(
            eventTypeCode,
            severityLevel,
            version,
            cancellationToken);

        if (riskMatrix is null)
        {
            _logger.LogWarning(
                "Risk matrix not found for event type. EventTypeCode: {eventTypeCode}, SeverityLevel: {severityLevel}, Version: {version}",
                eventTypeCode,
                severityLevel,
                version);
            return null;
        }

        var result = new RiskMatrixDTO
        {
            EventTypeCode = riskMatrix.EventType.Code,
            SeverityLevel = riskMatrix.SeverityLevel.ToString(),
            RiskLevel = riskMatrix.RiskLevel.ToString(),
            Version = riskMatrix.Version
        };

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1), cancellationToken);

        _logger.LogInformation(
            "Risk matrix retrieved successfully. EventTypeCode: {eventTypeCode}, SeverityLevel: {severityLevel}, RiskLevel: {riskLevel}, Version: {version}",
            riskMatrix.EventType.Code,
            riskMatrix.SeverityLevel,
            riskMatrix.RiskLevel,
            riskMatrix.Version);

        return result;
    }

    public async Task<IDFCurvesDTO?> GetIDFCurvesForEventTypeAsync(
        string eventTypeCode,
        int? returnPeriodYears,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{IDFCurveCachePrefix}:{eventTypeCode}:{returnPeriodYears ?? 0}";

        _logger.LogInformation(
            "Retrieving IDF curves for event type. EventTypeCode: {eventTypeCode}, ReturnPeriodYears: {returnPeriodYears}",
            eventTypeCode,
            returnPeriodYears);

        var cachedResult = await _cacheService.GetAsync<IDFCurvesDTO>(cacheKey, cancellationToken);
        if (cachedResult != null)
        {
            _logger.LogInformation("IDF curve retrieved from cache. EventTypeCode: {eventTypeCode}", eventTypeCode);

            return cachedResult;
        }

        var IDFCurve = await _IDFCurveRepository.GetIDFCurveForEventTypeAsync(
            eventTypeCode,
            returnPeriodYears,
            cancellationToken);

        if (IDFCurve == null)
        {
            _logger.LogWarning(
                "IDF curve not found for event type. EventTypeCode: {eventTypeCode}, ReturnPeriodYears: {returnPeriodYears}",
                eventTypeCode,
                returnPeriodYears);

            return null;
        }

        var result = new IDFCurvesDTO
        {
            DurationMinutes = IDFCurve.DurationMinutes,
            Intensity = IDFCurve.Intensity,
            ReturnPeriodYears = IDFCurve.ReturnPeriodYears,
            Version = IDFCurve.Version,
        };

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1), cancellationToken);

        _logger.LogInformation(
            "IDF curve retrieved successfully. EventTypeCode: {eventTypeCode}, DurationMinutes: {durationMinutes}, Intensity: {intensity}, ReturnPeriodYears: {returnPeriodYears}, Version: {version}",
            IDFCurve.EventType.Code,
            IDFCurve.DurationMinutes,
            IDFCurve.Intensity,
            IDFCurve.ReturnPeriodYears,
            IDFCurve.Version);

        return result;
    }

    public async Task<RegionalRiskParametersDTO?> GetRegionalRiskParametersAsync(
        Guid regionId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{RegionalParameterCachePrefix}:{regionId}";

        _logger.LogInformation("Retrieving regional risk parameters. RegionId: {regionId}", regionId);

        var cachedResult = await _cacheService.GetAsync<RegionalRiskParametersDTO>(cacheKey, cancellationToken);
        if (cachedResult != null)
        {
            _logger.LogInformation("Regional risk parameters retrieved from cache. RegionId: {regionId}", regionId);

            return cachedResult;
        }

        var regionalParameter = await _regionalParameterRepository.GetByRegionIdAsync(regionId, cancellationToken);

        if (regionalParameter is null)
        {
            _logger.LogWarning("Regional risk parameters not found. RegionId: {regionId}", regionId);

            return null;
        }

        var result = new RegionalRiskParametersDTO
        {
            RegionId = regionalParameter.RegionId,
            AdjustmentFactor = regionalParameter.AdjustmentFactor,
            Description = regionalParameter.Description
        };

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1), cancellationToken);

        _logger.LogInformation(
            "Regional risk parameters retrieved successfully. RegionId: {regionId}, AdjustmentFactor: {adjustmentFactor}",
            regionalParameter.RegionId,
            regionalParameter.AdjustmentFactor);

        return result;
    }

    public async Task<bool> CreateRiskMatrixAsync(
        RiskMatrixDTO riskMatrixDto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating risk matrix. EventTypeCode: {eventTypeCode}, SeverityLevel: {severityLevel}, RiskLevel: {riskLevel}, Version: {version}",
            riskMatrixDto.EventTypeCode,
            riskMatrixDto.SeverityLevel,
            riskMatrixDto.RiskLevel,
            riskMatrixDto.Version);

        var eventType = await _eventTypeRepository.GetEventTypeByCode(riskMatrixDto.EventTypeCode, cancellationToken);

        if (eventType is null)
        {
            _logger.LogError(
                "Failed to create risk matrix. Event type does not exist. EventTypeCode: {eventTypeCode}",
                riskMatrixDto.EventTypeCode);

            throw new ArgumentException("Event type does not exist.");
        }

        var riskMatrixExist = await _riskMatrixRepository.GetRiskMatrixForEventTypeAsync(
            riskMatrixDto.EventTypeCode,
            riskMatrixDto.SeverityLevel,
            riskMatrixDto.Version,
            cancellationToken) is not null;

        if (riskMatrixExist)
        {
            _logger.LogWarning(
                "Failed to create risk matrix. Risk matrix already exists. EventTypeCode: {eventTypeCode}, SeverityLevel: {severityLevel}, Version: {version}",
                riskMatrixDto.EventTypeCode,
                riskMatrixDto.SeverityLevel,
                riskMatrixDto.Version);

            throw new ArgumentException(
                "Risk matrix already exists for the given event type, severity level, and version.");
        }

        var riskMatrix = new RiskMatrix
        {
            EventTypeId = eventType.Id,
            SeverityLevel = Enum.Parse<SeverityLevelEnum>(riskMatrixDto.SeverityLevel),
            RiskLevel = Enum.Parse<RiskLevelEnum>(riskMatrixDto.RiskLevel),
            Version = riskMatrixDto.Version
        };

        var isCreated = await _riskMatrixRepository.AddRiskMatrixAsync(riskMatrix, cancellationToken);

        if (isCreated)
        {
            var cachePattern = $"{RiskMatrixCachePrefix}:{riskMatrixDto.EventTypeCode}:{riskMatrixDto.SeverityLevel}:*";
            await _cacheService.RemoveByPatternAsync(cachePattern, cancellationToken);

            _logger.LogInformation(
                "Risk matrix created successfully. EventTypeCode: {eventTypeCode}, SeverityLevel: {severityLevel}, RiskLevel: {riskLevel}, Version: {version}",
                riskMatrixDto.EventTypeCode,
                riskMatrixDto.SeverityLevel,
                riskMatrixDto.RiskLevel,
                riskMatrixDto.Version);
        }
        else
        {
            _logger.LogError(
                "Failed to create risk matrix. Database operation returned false. EventTypeCode: {eventTypeCode}, SeverityLevel: {severityLevel}, Version: {version}",
                riskMatrixDto.EventTypeCode,
                riskMatrixDto.SeverityLevel,
                riskMatrixDto.Version);
        }

        return isCreated;
    }

    public async Task<bool> CreateIDFCurvesAsync(
        CreateIDFCurvesDTO idfCurvesDto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating IDF curve. EventTypeCode: {eventTypeCode}, DurationMinutes: {durationMinutes}, Intensity: {intensity}, ReturnPeriodYears: {returnPeriodYears}, Version: {version}",
            idfCurvesDto.EventTypeCode,
            idfCurvesDto.DurationMinutes,
            idfCurvesDto.Intensity,
            idfCurvesDto.ReturnPeriodYears,
            idfCurvesDto.Version);

        var eventType = await _eventTypeRepository.GetEventTypeByCode(idfCurvesDto.EventTypeCode, cancellationToken);

        if (eventType is null)
        {
            _logger.LogError(
                "Failed to create IDF curve. Event type does not exist. EventTypeCode: {eventTypeCode}",
                idfCurvesDto.EventTypeCode);

            throw new ArgumentException("Event type does not exist.");
        }

        var idfCurveExist = await _IDFCurveRepository.GetIDFCurveForDurationAndPeriod(
            idfCurvesDto.EventTypeCode,
            idfCurvesDto.DurationMinutes,
            idfCurvesDto.ReturnPeriodYears,
            cancellationToken) is not null;

        if (idfCurveExist)
        {
            _logger.LogWarning(
                "Failed to create IDF curve. IDF curve already exists. EventTypeCode: {eventTypeCode}, DurationMinutes: {durationMinutes}, ReturnPeriodYears: {returnPeriodYears}",
                idfCurvesDto.EventTypeCode,
                idfCurvesDto.DurationMinutes,
                idfCurvesDto.ReturnPeriodYears);

            throw new ArgumentException(
                "IDF Curve already exists for the given event type, duration minutes and return period years.");
        }

        var idfCurve = new IDFCurve
        {
            EventTypeId = eventType.Id,
            DurationMinutes = idfCurvesDto.DurationMinutes,
            Intensity = idfCurvesDto.Intensity,
            ReturnPeriodYears = idfCurvesDto.ReturnPeriodYears,
            Version = idfCurvesDto.Version
        };

        var isCreated = await _IDFCurveRepository.AddIDFCurveAsync(idfCurve, cancellationToken);

        if (isCreated)
        {
            var cachePattern = $"{IDFCurveCachePrefix}:{idfCurvesDto.EventTypeCode}:*";
            await _cacheService.RemoveByPatternAsync(cachePattern, cancellationToken);

            _logger.LogInformation(
                "IDF curve created successfully. EventTypeCode: {eventTypeCode}, DurationMinutes: {durationMinutes}, ReturnPeriodYears: {returnPeriodYears}, Version: {version}",
                idfCurvesDto.EventTypeCode,
                idfCurvesDto.DurationMinutes,
                idfCurvesDto.ReturnPeriodYears,
                idfCurvesDto.Version);
        }
        else
        {
            _logger.LogError(
                "Failed to create IDF curve. Database operation returned false. EventTypeCode: {eventTypeCode}, DurationMinutes: {durationMinutes}, ReturnPeriodYears: {returnPeriodYears}",
                idfCurvesDto.EventTypeCode,
                idfCurvesDto.DurationMinutes,
                idfCurvesDto.ReturnPeriodYears);
        }

        return isCreated;
    }

    public async Task<bool> CreateRegionalRiskParametersAsync(
        RegionalRiskParametersDTO regionalRiskParametersDto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating regional risk parameters. RegionId: {regionId}, AdjustmentFactor: {adjustmentFactor}",
            regionalRiskParametersDto.RegionId,
            regionalRiskParametersDto.AdjustmentFactor);

        var regionalParameterExist = await _regionalParameterRepository.GetByRegionIdAsync(
            regionalRiskParametersDto.RegionId,
            cancellationToken) is not null;

        if (regionalParameterExist)
        {
            _logger.LogWarning(
                "Failed to create regional risk parameters. Regional risk parameters already exist. RegionId: {regionId}",
                regionalRiskParametersDto.RegionId);

            throw new ArgumentException(
                "Regional risk parameters already exist for the given region.");
        }

        var regionalParameter = new RegionalParameter
        {
            RegionId = regionalRiskParametersDto.RegionId,
            AdjustmentFactor = regionalRiskParametersDto.AdjustmentFactor,
            Description = regionalRiskParametersDto.Description
        };

        var isCreated =
            await _regionalParameterRepository.AddRegionalParameterAsync(regionalParameter, cancellationToken);

        if (isCreated)
        {
            var cacheKey = $"{RegionalParameterCachePrefix}:{regionalRiskParametersDto.RegionId}";
            await _cacheService.RemoveAsync(cacheKey, cancellationToken);

            _logger.LogInformation(
                "Regional risk parameters created successfully. RegionId: {regionId}, AdjustmentFactor: {adjustmentFactor}",
                regionalRiskParametersDto.RegionId,
                regionalRiskParametersDto.AdjustmentFactor);
        }
        else
        {
            _logger.LogError(
                "Failed to create regional risk parameters. Database operation returned false. RegionId: {regionId}",
                regionalRiskParametersDto.RegionId);
        }

        return isCreated;
    }
}