using System.Data.Common;
using System.Net;
using System.Net.Sockets;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Polly.CircuitBreaker;
using Polly.Timeout;
using StackExchange.Redis;
using Twilio.Exceptions;

namespace ChannelsService.Infrastructure.Messaging.Reliability;

/// <summary>Failures worth retrying: the same operation may succeed once the dependency recovers.</summary>
public static class TransientFailureClassifier
{
    public static bool IsTransient(Exception exception) => exception switch
    {
        TimeoutException or SocketException or IOException => true,
        HttpRequestException { StatusCode: null } => true,
        HttpRequestException { StatusCode: { } status } => IsTransientStatus(status),
        // HttpClient timeouts surface as TaskCanceledException wrapping a TimeoutException.
        TaskCanceledException { InnerException: TimeoutException } => true,
        TimeoutRejectedException or BrokenCircuitException => true,
        ApiException { Status: >= 500 or 408 or 429 } => true,
        KafkaException kafkaException => !kafkaException.Error.IsFatal,
        DbException { IsTransient: true } => true,
        DbUpdateConcurrencyException => true,
        RedisConnectionException => true,
        _ => exception.InnerException is not null && IsTransient(exception.InnerException)
    };

    public static bool IsTransientStatus(HttpStatusCode status) =>
        (int)status >= 500 || status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;
}
