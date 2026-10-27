using System.Net.Sockets;
using Confluent.Kafka;
using Geospatial.Infrastructure.Persistence.Repositories;

namespace Geospatial.Infrastructure.Messaging.Reliability;

/// <summary>Failures worth retrying: the same message may succeed once the dependency recovers.</summary>
public static class TransientFailureClassifier
{
    public static bool IsTransient(Exception exception) => exception switch
    {
        TimeoutException or SocketException or IOException => true,
        ElasticsearchUnavailableException => true,
        KafkaException kafkaException => !kafkaException.Error.IsFatal,
        _ => exception.InnerException is not null && IsTransient(exception.InnerException)
    };
}
