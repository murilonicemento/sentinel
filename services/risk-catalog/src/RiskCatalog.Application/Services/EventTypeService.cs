using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Interfaces;
using RiskCatalog.Application.Services.Interfaces;
using RiskCatalog.Domain.Enums;
using RiskCatalog.Domain.EventTypes;
using RiskCatalog.Domain.IRepositories;

namespace RiskCatalog.Application.Services;

public class EventTypeService : IEventTypeService
{
    private readonly IEventTypeRepository _eventTypeRepository;
    private readonly ISeverityRepository _severityRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<EventTypeService> _logger;
    private const string EventTypeCachePrefix = "event-type";
    private const string EventTypeListCachePrefix = "event-types";
    private const string SeverityCriterionCachePrefix = "severity-criterion";

    public EventTypeService(
        IEventTypeRepository eventTypeRepository,
        ISeverityRepository severityRepository,
        ICacheService cacheService,
        ILogger<EventTypeService> logger)
    {
        _eventTypeRepository = eventTypeRepository;
        _severityRepository = severityRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<List<EventTypeDTO>> GetEventTypesAsync(
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{EventTypeListCachePrefix}:{(isActive.HasValue ? isActive.Value : "all")}";

        _logger.LogInformation("Retrieving event types. IsActive: {isActive}", isActive);

        var cachedResult = await _cacheService.GetAsync<List<EventTypeDTO>>(cacheKey, cancellationToken);
        if (cachedResult != null)
        {
            _logger.LogInformation("Event types retrieved from cache. IsActive: {isActive}", isActive);

            return cachedResult;
        }

        var eventTypes = await _eventTypeRepository.GetEventTypes(isActive, cancellationToken);
        var eventTypeDTOs = eventTypes.Select(e => new EventTypeDTO
        {
            Id = e.Id,
            Code = e.Code,
            Name = e.Name,
        }).ToList();

        await _cacheService.SetAsync(cacheKey, eventTypeDTOs, TimeSpan.FromHours(1), cancellationToken);

        _logger.LogInformation(
            "Event types retrieved successfully. Count: {count}, IsActive: {isActive}",
            eventTypeDTOs.Count,
            isActive);

        return eventTypeDTOs;
    }

    public async Task<EventTypeDTO?> GetEventTypeByCodeAsync(
        string eventTypeCode,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{EventTypeCachePrefix}:{eventTypeCode}";

        _logger.LogInformation("Retrieving event type by code. EventTypeCode: {eventTypeCode}", eventTypeCode);

        var cachedResult = await _cacheService.GetAsync<EventTypeDTO>(cacheKey, cancellationToken);

        if (cachedResult != null)
        {
            _logger.LogInformation("Event type retrieved from cache. EventTypeCode: {eventTypeCode}", eventTypeCode);

            return cachedResult;
        }

        var eventType = await _eventTypeRepository.GetEventTypeByCode(eventTypeCode, cancellationToken);

        if (eventType is null)
        {
            _logger.LogWarning("Event type not found. EventTypeCode: {eventTypeCode}", eventTypeCode);

            return null;
        }

        var result = new EventTypeDTO
        {
            Id = eventType.Id,
            Code = eventType.Code,
            Name = eventType.Name,
        };

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1), cancellationToken);

        _logger.LogInformation(
            "Event type retrieved successfully. EventTypeCode: {eventTypeCode}, Name: {name}, IsActive: {isActive}",
            eventType.Code,
            eventType.Name,
            eventType.IsActive);

        return result;
    }

    public async Task<List<SeverityCriterionDTO>> GetSeverityCriterionForEventTypeAsync(
        string eventTypeCode,
        int? version,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{SeverityCriterionCachePrefix}:{eventTypeCode}:{version ?? 0}";

        _logger.LogInformation(
            "Retrieving severity criteria for event type. EventTypeCode: {eventTypeCode}, Version: {version}",
            eventTypeCode,
            version);

        var cachedResult = await _cacheService.GetAsync<List<SeverityCriterionDTO>>(cacheKey, cancellationToken);

        if (cachedResult != null)
        {
            _logger.LogInformation("Severity criteria retrieved from cache. EventTypeCode: {eventTypeCode}",
                eventTypeCode);

            return cachedResult;
        }

        var severitiesCriterion = await _eventTypeRepository.GetSeveritiesCriterionForEventType(
            eventTypeCode,
            version,
            cancellationToken);
        var severityCriterionDTOs = severitiesCriterion.Select(severityCriterion => new SeverityCriterionDTO
        {
            EventTypeCode = eventTypeCode,
            SeverityLevel = severityCriterion.Severity.Level.ToString(),
            MinValue = severityCriterion.MinValue,
            MaxValue = severityCriterion.MaxValue,
            Unit = severityCriterion.Unit,
            Version = severityCriterion.Version,
        }).ToList();

        await _cacheService.SetAsync(cacheKey, severityCriterionDTOs, TimeSpan.FromHours(1), cancellationToken);

        _logger.LogInformation(
            "Severity criteria retrieved successfully. EventTypeCode: {eventTypeCode}, Count: {count}, Version: {version}",
            eventTypeCode,
            severityCriterionDTOs.Count,
            version);

        return severityCriterionDTOs;
    }

    public async Task<bool> CreateEventTypeAsync(
        CreateEventTypeDTO eventTypeDto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating event type. Code: {code}, Name: {name}, IsActive: {isActive}",
            eventTypeDto.Code,
            eventTypeDto.Name,
            eventTypeDto.IsActive);

        var eventTypeExists =
            await _eventTypeRepository.GetEventTypeByCode(eventTypeDto.Code, cancellationToken) is not null;

        if (eventTypeExists)
        {
            _logger.LogWarning(
                "Failed to create event type. Event type already exists. Code: {code}",
                eventTypeDto.Code);

            throw new ArgumentException("Event type with the same code already exists.");
        }

        var eventType = new EventType
        {
            Code = eventTypeDto.Code,
            Name = eventTypeDto.Name,
            Description = eventTypeDto.Description,
            IsActive = eventTypeDto.IsActive
        };
        var isCreated = await _eventTypeRepository.CreateEventType(eventType, cancellationToken);

        if (isCreated)
        {
            await _cacheService.RemoveByPatternAsync($"{EventTypeListCachePrefix}:*", cancellationToken);
            await _cacheService.RemoveByPatternAsync($"{EventTypeCachePrefix}:*", cancellationToken);

            _logger.LogInformation(
                "Event type created successfully. Code: {code}, Name: {name}, IsActive: {isActive}",
                eventTypeDto.Code,
                eventTypeDto.Name,
                eventTypeDto.IsActive);
        }
        else
        {
            _logger.LogError(
                "Failed to create event type. Database operation returned false. Code: {code}",
                eventTypeDto.Code);
        }

        return isCreated;
    }

    public async Task<bool> CreateSeverity(SeverityDTO severityDto, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating severity. Level: {level}, Description: {description}",
            severityDto.Level,
            severityDto.Description
        );
        var severityLevel = Enum.Parse<SeverityLevelEnum>(severityDto.Level);
        var severityExists = await _severityRepository.GetByLevel(severityLevel, cancellationToken) is not null;

        if (severityExists)
        {
            _logger.LogWarning(
                "Failed to create severity. Severity level already exists. Level: {level}",
                severityDto.Level);

            throw new ArgumentException("Severity with the same level already exists.");
        }

        var severity = new Severity
        {
            Level = severityLevel,
            Description = severityDto.Description
        };
        var isCreated = await _severityRepository.CreateSeverity(severity, cancellationToken);

        if (isCreated)
        {
            _logger.LogInformation(
                "Severity created successfully. Level: {level}, Description: {description}",
                severityDto.Level,
                severityDto.Description);
        }
        else
        {
            _logger.LogError(
                "Failed to create severity. Database operation returned false. Level: {level}",
                severityDto.Level);
        }

        return isCreated;
    }

    public async Task<bool> UpdateEventTypeStatusAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating event type status. Id: {id}, IsActive: {isActive}",
            id,
            isActive);

        var isUpdated = await _eventTypeRepository.UpdateEventTypeStatus(id, isActive, cancellationToken);

        if (isUpdated)
        {
            await _cacheService.RemoveByPatternAsync($"{EventTypeListCachePrefix}:*", cancellationToken);
            await _cacheService.RemoveByPatternAsync($"{EventTypeCachePrefix}:*", cancellationToken);

            _logger.LogInformation(
                "Event type status updated successfully. Id: {id}, IsActive: {isActive}",
                id,
                isActive);
        }
        else
        {
            _logger.LogWarning(
                "Failed to update event type status. Event type not found or update failed. Id: {id}, IsActive: {isActive}",
                id,
                isActive);
        }

        return isUpdated;
    }

    public async Task<bool> CreateSeverityCriterionToEventType(
        SeverityCriterionDTO severityCriterionDto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Adding severity criterion to event type. EventTypeCode: {eventTypeCode}, SeverityLevel: {severityLevel}, MinValue: {minValue}, MaxValue: {maxValue}, Unit: {unit}, Version: {version}",
            severityCriterionDto.EventTypeCode,
            severityCriterionDto.SeverityLevel,
            severityCriterionDto.MinValue,
            severityCriterionDto.MaxValue,
            severityCriterionDto.Unit,
            severityCriterionDto.Version);

        var eventType =
            await _eventTypeRepository.GetEventTypeByCode(severityCriterionDto.EventTypeCode, cancellationToken);

        if (eventType is null)
        {
            _logger.LogError(
                "Failed to add severity criterion. Event type not found. EventTypeCode: {eventTypeCode}",
                severityCriterionDto.EventTypeCode);

            throw new KeyNotFoundException("Event type not found.");
        }

        var severityCriteria = eventType.SeverityCriteria.Where(x => x.EventTypeId == eventType.Id).ToList();

        if (severityCriteria.Any(severityCriterion =>
                severityCriterionDto.MinValue <= severityCriterion.MaxValue &&
                severityCriterionDto.MaxValue >= severityCriterion.MinValue &&
                severityCriterionDto.Unit == severityCriterion.Unit &&
                severityCriterionDto.Version == severityCriterion.Version))
        {
            _logger.LogWarning(
                "Failed to add severity criterion. Range overlaps with existing criterion. EventTypeCode: {eventTypeCode}, MinValue: {minValue}, MaxValue: {maxValue}, Unit: {unit}, Version: {version}",
                severityCriterionDto.EventTypeCode,
                severityCriterionDto.MinValue,
                severityCriterionDto.MaxValue,
                severityCriterionDto.Unit,
                severityCriterionDto.Version);

            throw new BadHttpRequestException("Severity criterion range overlaps with an existing criterion.");
        }

        var severityLevel = Enum.Parse<SeverityLevelEnum>(severityCriterionDto.SeverityLevel);
        var severity = await _severityRepository.GetByLevel(severityLevel, cancellationToken);

        if (severity is null)
        {
            _logger.LogError(
                "Failed to add severity criterion. Severity level not found. SeverityLevel: {severityLevel}",
                severityCriterionDto.SeverityLevel);

            throw new ArgumentException("Severity level not found.");
        }

        var severityCriterion = new SeverityCriterion
        {
            EventTypeId = eventType.Id,
            SeverityId = severity.Id,
            MinValue = severityCriterionDto.MinValue,
            MaxValue = severityCriterionDto.MaxValue,
            Unit = severityCriterionDto.Unit,
            Version = severityCriterionDto.Version
        };

        var isAdded = await _eventTypeRepository.AddSeverityCriterionToEventType(severityCriterion, cancellationToken);

        if (isAdded)
        {
            var cachePattern = $"{SeverityCriterionCachePrefix}:{severityCriterionDto.EventTypeCode}:*";
            await _cacheService.RemoveByPatternAsync(cachePattern, cancellationToken);

            _logger.LogInformation(
                "Severity criterion added successfully. EventTypeCode: {eventTypeCode}, SeverityLevel: {severityLevel}, Version: {version}",
                severityCriterionDto.EventTypeCode,
                severityCriterionDto.SeverityLevel,
                severityCriterionDto.Version);
        }
        else
        {
            _logger.LogError(
                "Failed to add severity criterion. Database operation returned false. EventTypeCode: {eventTypeCode}, SeverityLevel: {severityLevel}",
                severityCriterionDto.EventTypeCode,
                severityCriterionDto.SeverityLevel);
        }

        return isAdded;
    }
}