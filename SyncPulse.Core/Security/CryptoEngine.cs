using System;
using System.Security.Cryptography;
using System.Text;

namespace SyncPulse.Core.Security
{
    /// <summary>
    /// محرك التشفير وتجزئة كلمات المرور وحماية الجلسات (NIST SP 800-63B / SP 800-132)
    /// </summary>
    public static class CryptoEngine
    {
        private const int SaltSize = 16; // 128-bit cryptographic salt
        private const int HashSize = 32; // 256-bit hash
        private const int Iterations = 10000; // PBKDF2 iteration count

        /// <summary>
        /// توليد Salt عشوائي مشفر فريد لكل مستخدم
        /// </summary>
        public static string GenerateSalt()
        {
            byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
            return Convert.ToBase64String(saltBytes);
        }

        /// <summary>
        /// تجزئة كلمة المرور باستخدام PBKDF2 و SHA-256 مع الـ Salt
        /// </summary>
        public static string HashPassword(string password, string salt)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
            if (string.IsNullOrEmpty(salt)) throw new ArgumentNullException(nameof(salt));

            byte[] saltBytes = Convert.FromBase64String(salt);
            byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                saltBytes,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize
            );

            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// التحقق من مطابقة كلمة المرور مع الهاش في زمن ثابت (Constant-Time Verification) لمنع هجمات التوقيت
        /// </summary>
        public static bool VerifyPassword(string password, string salt, string expectedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(expectedHash))
                return false;

            try
            {
                string computedHash = HashPassword(password, salt);
                byte[] computedBytes = Convert.FromBase64String(computedHash);
                byte[] expectedBytes = Convert.FromBase64String(expectedHash);

                return CryptographicOperations.FixedTimeEquals(computedBytes, expectedBytes);
            }
            catch
            {
                return false;
            }
        }
    }
}
