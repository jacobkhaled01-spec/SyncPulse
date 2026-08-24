using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SyncPulse.Core.Packets;
using SyncPulse.Core.Security;

namespace SyncPulse.Server.Data
{
    public class UserRepository
    {
        private readonly DatabaseManager _db;

        public UserRepository(DatabaseManager db)
        {
            _db = db;
        }

        public async Task<(bool Success, int UserID, string Message)> RegisterUserAsync(string username, string password, string displayName)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return (false, 0, "اسم المستخدم وكلمة المرور مطلوبة.");

            username = username.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName.Trim();

            using var conn = await _db.CreateConnectionAsync();

            // 1. التحقق من فرادة اسم المستخدم
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM USERS WHERE Username = @u";
            checkCmd.Parameters.AddWithValue("@u", username);
            long count = (long)(await checkCmd.ExecuteScalarAsync() ?? 0L);

            if (count > 0)
                return (false, 0, "اسم المستخدم مستخدم مسبقاً، يرجى اختيار اسم آخر.");

            // 2. تجزئة كلمة المرور مع Salt عشوائي فريد
            string salt = CryptoEngine.GenerateSalt();
            string hash = CryptoEngine.HashPassword(password, salt);
            string now = DateTime.UtcNow.ToString("o");

            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO USERS (Username, DisplayName, PasswordHash, Salt, CreatedAt, IsActive)
                VALUES (@u, @d, @h, @s, @c, 1);
                SELECT last_insert_rowid();
            ";
            insertCmd.Parameters.AddWithValue("@u", username);
            insertCmd.Parameters.AddWithValue("@d", displayName);
            insertCmd.Parameters.AddWithValue("@h", hash);
            insertCmd.Parameters.AddWithValue("@s", salt);
            insertCmd.Parameters.AddWithValue("@c", now);

            long userId = (long)(await insertCmd.ExecuteScalarAsync() ?? 0L);
            return (true, (int)userId, "تم إنشاء الحساب بنجاح.");
        }

        public async Task<(bool Success, int UserID, string Username, string DisplayName, string Message)> AuthenticateUserAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return (false, 0, "", "", "اسم المستخدم وكلمة المرور مطلوبة.");

            username = username.Trim();
            using var conn = await _db.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT UserID, Username, DisplayName, PasswordHash, Salt, IsActive FROM USERS WHERE Username = @u";
            cmd.Parameters.AddWithValue("@u", username);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return (false, 0, "", "", "اسم المستخدم أو كلمة المرور غير صحيحة.");

            int userId = reader.GetInt32(0);
            string dbUsername = reader.GetString(1);
            string displayName = reader.GetString(2);
            string passwordHash = reader.GetString(3);
            string salt = reader.GetString(4);
            bool isActive = reader.GetInt32(5) == 1;

            if (!isActive)
                return (false, 0, "", "", "هذا الحساب محظور حالياً من قبل إدارة الخادم.");

            if (!CryptoEngine.VerifyPassword(password, salt, passwordHash))
                return (false, 0, "", "", "اسم المستخدم أو كلمة المرور غير صحيحة.");

            return (true, userId, dbUsername, displayName, "تم تسجيل الدخول بنجاح.");
        }

        public async Task<UserContactItem?> SearchUserByUsernameAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;

            username = username.Trim();
            using var conn = await _db.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT UserID, Username, DisplayName, AvatarPath, LastSeenAt FROM USERS WHERE Username = @u AND IsActive = 1";
            cmd.Parameters.AddWithValue("@u", username);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new UserContactItem
                {
                    UserID = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    DisplayName = reader.GetString(2),
                    AvatarPath = reader.IsDBNull(3) ? null : reader.GetString(3),
                    LastSeenAt = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4))
                };
            }

            return null;
        }

        public async Task UpdateLastSeenAsync(int userId)
        {
            using var conn = await _db.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE USERS SET LastSeenAt = @now WHERE UserID = @id";
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@id", userId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<UserContactItem>> GetAllUsersAsync()
        {
            var list = new List<UserContactItem>();
            using var conn = await _db.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT UserID, Username, DisplayName, AvatarPath, LastSeenAt, IsActive FROM USERS ORDER BY UserID DESC";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new UserContactItem
                {
                    UserID = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    DisplayName = reader.GetString(2),
                    AvatarPath = reader.IsDBNull(3) ? null : reader.GetString(3),
                    LastSeenAt = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4))
                });
            }

            return list;
        }
    }
}
