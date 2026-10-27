using System.Security.Cryptography;
using System.Text;

namespace Ingestion.Application.Services;

/// <summary>Derives a stable event id from the identity of a collection sample (name-based, RFC 9562 version 8).</summary>
public static class DeterministicEventId
{
    public static Guid For(string collectionKey, int sampleIndex)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{collectionKey}#{sampleIndex}"));
        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80); // version 8
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 9562 variant
        return new Guid(bytes, bigEndian: true);
    }
}
