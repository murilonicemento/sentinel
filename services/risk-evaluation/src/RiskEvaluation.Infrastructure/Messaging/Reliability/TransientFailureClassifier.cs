using System.Net;
using System.Net.Sockets;
using Confluent.Kafka;
using MongoDB.Driver;
using StackExchange.Redis;

namespace RiskEvaluation.Infrastructure.Messaging.Reliability;

/// <summary>Failures worth retrying: the same message may succeed once the dependency recovers.</summary>
public static class TransientFailureClassifier
{
    public static bool IsTransient(Exception exception) => exception switch
    {
        TimeoutException or SocketException or IOException => true,
        HttpRequestException { StatusCode: null } => true,
        HttpRequestException { StatusCode: >= HttpStatusCode.InternalServerError or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests } => true,
        // HttpClient timeouts surface as TaskCanceledException wrapping a TimeoutException.
        TaskCanceledException { InnerException: TimeoutException } => true,
        MongoConnectionException or MongoExecutionTimeoutException or MongoNotPrimaryException
            or MongoNodeIsRecoveringException or MongoWaitQueueFullException or MongoWriteConcernException => true,
        RedisConnectionException => true,
        KafkaException kafkaException => !kafkaException.Error.IsFatal,
        MessageInProgressException => true,
        _ => exception.InnerException is not null && IsTransient(exception.InnerException)
    };
}
