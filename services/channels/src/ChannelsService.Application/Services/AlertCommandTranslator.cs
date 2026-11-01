using System.Globalization;
using ChannelsService.Application.DTOs;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Events;

namespace ChannelsService.Application.Services;

/// <summary>
/// Turns an alert-orchestrator trigger into a notification. The orchestrator decides that an alert is due; which
/// channels deliver it comes from the tenant's channel settings unless the command names them.
/// </summary>
public static class AlertCommandTranslator
{
    public const string AlertTriggeredEventType = "AlertTriggered";

    /// <summary>
    /// One notification per alert window and escalation level: a redelivered command keeps the same event id, so the
    /// delivery skips the channels that already received it, while each escalation is delivered again.
    /// </summary>
    public static NotificationEvent ToNotification(TriggerAlertCommandDTO command)
    {
        var score = command.RiskScore.ToString("0.00", CultureInfo.InvariantCulture);
        return new NotificationEvent
        {
            EventId = $"alert:{command.AlertWindowId:N}:{command.EscalationLevel}",
            TenantId = command.TenantId ?? string.Empty,
            EventType = AlertTriggeredEventType,
            CorrelationId = command.AlertWindowId.ToString(),
            PriorityEnum = command.EscalationLevel >= 2 ? NotificationPriorityEnum.Critical : NotificationPriorityEnum.High,
            Message = new NotificationMessage
            {
                Title = string.IsNullOrWhiteSpace(command.Title)
                    ? $"Alerta de {command.RiskType} em {command.Region}"
                    : command.Title,
                Body = string.IsNullOrWhiteSpace(command.Message)
                    ? $"Risco de {command.RiskType} na região {command.Region} (score {score}, nível de " +
                      $"escalonamento {command.EscalationLevel})."
                    : command.Message
            },
            Channels = (command.TargetChannels ?? [])
                .Select(name => Enum.TryParse<ChannelTypeEnum>(name, true, out var channel) ? channel : (ChannelTypeEnum?)null)
                .OfType<ChannelTypeEnum>()
                .Distinct()
                .ToList(),
            Metadata = new Dictionary<string, string>
            {
                ["alertWindowId"] = command.AlertWindowId.ToString(),
                ["region"] = command.Region,
                ["riskType"] = command.RiskType,
                ["riskScore"] = score,
                ["signalCount"] = command.SignalCount.ToString(CultureInfo.InvariantCulture),
                ["escalationLevel"] = command.EscalationLevel.ToString(CultureInfo.InvariantCulture),
                ["triggeredAt"] = command.TriggeredAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
            }
        };
    }
}
