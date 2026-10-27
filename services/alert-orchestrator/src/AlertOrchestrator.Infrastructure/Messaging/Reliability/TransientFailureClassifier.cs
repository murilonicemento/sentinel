using System.Data.Common;
using System.Net.Sockets;
using AlertOrchestrator.Application.Interfaces.Messaging;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace AlertOrchestrator.Infrastructure.Messaging.Reliability;

/// <summary>Failures worth retrying: the same message may succeed once the dependency recovers.</summary>
public static class TransientFailureClassifier
{
    public static bool IsTransient(Exception exception) => exception switch
    {
        TimeoutException or SocketException or IOException => true,
        HttpRequestException { StatusCode: null or >= System.Net.HttpStatusCode.InternalServerError } => true,
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.RequestTimeout or System.Net.HttpStatusCode.TooManyRequests } => true,
        // HttpClient timeouts surface as TaskCanceledException wrapping a TimeoutException.
        TaskCanceledException { InnerException: TimeoutException } => true,
        KafkaException kafkaException => !kafkaException.Error.IsFatal,
        DbException { IsTransient: true } => true,
        // Another writer changed the aggregate; reprocessing reloads it.
        DbUpdateConcurrencyException => true,
        RedisConnectionException => true,
        MessageInProgressException => true,
        _ => exception.InnerException is not null && IsTransient(exception.InnerException)
    };
}
