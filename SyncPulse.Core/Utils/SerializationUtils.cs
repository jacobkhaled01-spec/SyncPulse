using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SyncPulse.Core.Utils
{
    /// <summary>
    /// أدوات التسلسل السريع (JSON Serialization Engine)
    /// </summary>
    public static class SerializationUtils
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        public static byte[] SerializeToUtf8Bytes<T>(T value)
        {
            if (value == null) return Array.Empty<byte>();
            return JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        }

        public static T? DeserializeFromUtf8Bytes<T>(ReadOnlySpan<byte> bytes)
        {
            if (bytes.IsEmpty) return default;
            return JsonSerializer.Deserialize<T>(bytes, JsonOptions);
        }

        public static string ToJsonString<T>(T value)
        {
            if (value == null) return string.Empty;
            return JsonSerializer.Serialize(value, JsonOptions);
        }

        public static T? FromJsonString<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return default;
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
    }
}
