using System.Data.Common;
using System.Net.Sockets;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;

namespace TenantsBilling.Infrastructure.Messaging.Reliability;

/// <summary>Failures worth retrying: the same message may succeed once the dependency recovers.</summary>
public static class TransientFailureClassifier
{
    public static bool IsTransient(Exception exception) => exception switch
    {
        TimeoutException or SocketException or IOException => true,
        KafkaException kafkaException => !kafkaException.Error.IsFatal,
        DbException { IsTransient: true } => true,
        DbUpdateConcurrencyException => true,
        _ => exception.InnerException is not null && IsTransient(exception.InnerException)
    };
}
