using System;
using System.Security.Cryptography;
using System.Text;
using SyncPulse.Core.Utils;

namespace SyncPulse.Core.Security
{
    public class JwtPayload
    {
        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public long ExpiredAtUnix { get; set; }
    }

    /// <summary>
    /// محرك إصدار والتحقق من رموز الجلسات المشفرة (JWT Engine - RFC 7519)
    /// </summary>
    public static class JwtTokenEngine
    {
        private static readonly byte[] SecretKey = Encoding.UTF8.GetBytes("SyncPulse_SecureLocalSecretKey_2026_HighPerfTokenKey_#77");

        public static string GenerateToken(int userId, string username, TimeSpan validFor)
        {
            var header = new { alg = "HS256", typ = "JWT" };
            var payload = new JwtPayload
            {
                UserID = userId,
                Username = username,
                ExpiredAtUnix = DateTimeOffset.UtcNow.Add(validFor).ToUnixTimeSeconds()
            };

            string headerBase64 = Convert.ToBase64String(SerializationUtils.SerializeToUtf8Bytes(header));
            string payloadBase64 = Convert.ToBase64String(SerializationUtils.SerializeToUtf8Bytes(payload));

            string dataToSign = $"{headerBase64}.{payloadBase64}";
            using var hmac = new HMACSHA256(SecretKey);
            byte[] signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
            string signatureBase64 = Convert.ToBase64String(signatureBytes);

            return $"{dataToSign}.{signatureBase64}";
        }

        public static bool ValidateToken(string token, out JwtPayload? payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(token)) return false;

            string[] parts = token.Split('.');
            if (parts.Length != 3) return false;

            string dataToSign = $"{parts[0]}.{parts[1]}";
            using var hmac = new HMACSHA256(SecretKey);
            byte[] expectedSig = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
            string expectedSigBase64 = Convert.ToBase64String(expectedSig);

            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSigBase64),
                Encoding.UTF8.GetBytes(parts[2])))
            {
                return false;
            }

            try
            {
                byte[] payloadBytes = Convert.FromBase64String(parts[1]);
                payload = SerializationUtils.DeserializeFromUtf8Bytes<JwtPayload>(payloadBytes);

                if (payload == null) return false;
                long currentUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return payload.ExpiredAtUnix > currentUnix;
            }
            catch
            {
                return false;
            }
        }
    }
}
