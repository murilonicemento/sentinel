using System.Net.Sockets;
using Confluent.Kafka;
using MongoDB.Driver;

namespace AuditCompliance.Infrastructure.Messaging;

public static class TransientFailureClassifier
{
    public static bool IsTransient(Exception exception)
    {
        return exception switch
        {
            MongoConnectionException or MongoExecutionTimeoutException or MongoNotPrimaryException
                or MongoNodeIsRecoveringException or MongoWaitQueueFullException or MongoWriteConcernException => true,
            TimeoutException or SocketException or IOException => true,
            KafkaException kafkaException => !kafkaException.Error.IsFatal,
            _ => exception.InnerException is not null && IsTransient(exception.InnerException)
        };
    }
}
