using AlertOrchestrator.Application.Commands;
using AlertOrchestrator.Application.DTOs;
using AlertOrchestrator.Application.Events;
using AlertOrchestrator.Application.Handlers;
using AlertOrchestrator.Application.Interfaces.Messaging;
using AlertOrchestrator.Application.Interfaces.Observability;
using AlertOrchestrator.Application.Ports;
using AlertOrchestrator.Domain.Aggregates;
using AlertOrchestrator.Domain.Configuration;
using AlertOrchestrator.Domain.Events;
using AlertOrchestrator.Domain.Interfaces.Repositories;
using AlertOrchestrator.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace AlertOrchestrator.UnitTests.Handlers;

public class RiskUpdatedEventHandlerTests
{
    private readonly Mock<IAlertWindowRepository> _windowRepository = new();
    private readonly Mock<IEventPublisher> _eventPublisher = new();
    private readonly Mock<ICommandPublisher> _commandPublisher = new();
    private readonly Mock<IIdempotencyService> _idempotencyService = new();
    private readonly Mock<IAlertConfigurationPort> _configurationPort = new();
    private readonly Mock<IAlertMetrics> _metrics = new();
    private readonly Mock<ILogger<RiskUpdatedEventHandler>> _logger = new();

    private RiskUpdatedEventHandler CreateHandler() => new(
        _windowRepository.Object,
        _eventPublisher.Object,
        _commandPublisher.Object,
        _idempotencyService.Object,
        _configurationPort.Object,
        _metrics.Object,
        _logger.Object);

    [Fact]
    public async Task Handle_WhenRiskScoreBelowThreshold_ShouldOpenWindowWithoutTriggeringAlert()
    {
        var notification = new RiskUpdatedEvent(
            Guid.NewGuid(),
            "region-1",
            "flood",
            50,
            "tenant-1",
            DateTime.UtcNow,
            new Dictionary<string, string>());

        _idempotencyService
            .Setup(x => x.IsProcessedAsync(notification.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _configurationPort
            .Setup(x => x.GetConfigurationAsync(notification.RiskType, notification.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TriggerRuleConfiguration(
                100,
                TimeSpan.FromMinutes(30),
                1,
                1,
                RequireSensor: false,
                RequireSatellite: false));

        _windowRepository
            .Setup(x => x.GetOpenWindowAsync(notification.Region, It.IsAny<RiskType>(), notification.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertWindow?)null);

        _windowRepository
            .Setup(x => x.AddAsync(It.IsAny<AlertWindow>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _windowRepository
            .Setup(x => x.UpdateAsync(It.IsAny<AlertWindow>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        await handler.Handle(notification, CancellationToken.None);

        _windowRepository.Verify(x => x.AddAsync(It.IsAny<AlertWindow>(), It.IsAny<CancellationToken>()), Times.Never);
        _windowRepository.Verify(x => x.UpdateAsync(It.IsAny<AlertWindow>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventPublisher.Verify(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        _commandPublisher.Verify(x => x.PublishAsync(It.IsAny<TriggerAlertCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _idempotencyService.Verify(x => x.MarkAsProcessedAsync(notification.EventId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRiskScoreAboveThreshold_ShouldTriggerAlertAndPublishCommand()
    {
        var notification = new RiskUpdatedEvent(
            Guid.NewGuid(),
            "region-1",
            "flood",
            150,
            "tenant-1",
            DateTime.UtcNow,
            new Dictionary<string, string>());

        _idempotencyService
            .Setup(x => x.IsProcessedAsync(notification.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _configurationPort
            .Setup(x => x.GetConfigurationAsync(notification.RiskType, notification.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TriggerRuleConfiguration(
                100,
                TimeSpan.FromMinutes(30),
                1,
                1,
                RequireSensor: false,
                RequireSatellite: false));

        _windowRepository
            .Setup(x => x.GetOpenWindowAsync(notification.Region, It.IsAny<RiskType>(), notification.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertWindow?)null);

        _windowRepository
            .Setup(x => x.AddAsync(It.IsAny<AlertWindow>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _windowRepository
            .Setup(x => x.UpdateAsync(It.IsAny<AlertWindow>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _eventPublisher
            .Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _commandPublisher
            .Setup(x => x.PublishAsync(It.IsAny<TriggerAlertCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        await handler.Handle(notification, CancellationToken.None);

        _windowRepository.Verify(x => x.AddAsync(It.IsAny<AlertWindow>(), It.IsAny<CancellationToken>()), Times.Once);
        _windowRepository.Verify(x => x.UpdateAsync(It.IsAny<AlertWindow>(), It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisher.Verify(x => x.PublishAsync(It.Is<object>(o => o is AlertTriggeredEvent), It.IsAny<CancellationToken>()), Times.Once);
        _commandPublisher.Verify(x => x.PublishAsync(It.IsAny<TriggerAlertCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _idempotencyService.Verify(x => x.MarkAsProcessedAsync(notification.EventId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
