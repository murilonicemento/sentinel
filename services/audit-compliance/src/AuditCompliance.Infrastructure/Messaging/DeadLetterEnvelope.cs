namespace AuditCompliance.Infrastructure.Messaging;

/// <summary>
/// Mensagem publicada no tópico de dead-letter. Preserva o payload original para reprocessamento
/// controlado via tópico de replay; o acesso ao tópico deve ser restrito aos operadores.
/// </summary>
public sealed record DeadLetterEnvelope
{
    public required string OriginalTopic { get; init; }
    public int Partition { get; init; }
    public long Offset { get; init; }
    public string? Key { get; init; }
    public required string Payload { get; init; }
    public required string ErrorCode { get; init; }
    public required string ErrorReason { get; init; }
    public DateTimeOffset FailedAt { get; init; }
    public required string ConsumerGroup { get; init; }
}
