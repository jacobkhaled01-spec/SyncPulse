using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SyncPulse.Core.Packets;

namespace SyncPulse.Server.Data
{
    public class ContactRepository
    {
        private readonly DatabaseManager _db;

        public ContactRepository(DatabaseManager db)
        {
            _db = db;
        }

        public async Task<bool> AddContactAsync(int ownerUserId, int contactUserId, string? customName)
        {
            if (ownerUserId == contactUserId) return false;

            using var conn = await _db.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO USER_CONTACTS (OwnerUserID, ContactUserID, CustomName, AddedAt)
                VALUES (@o, @c, @n, @now)
                ON CONFLICT(OwnerUserID, ContactUserID) DO UPDATE SET CustomName = @n;
            ";
            cmd.Parameters.AddWithValue("@o", ownerUserId);
            cmd.Parameters.AddWithValue("@c", contactUserId);
            cmd.Parameters.AddWithValue("@n", (object?)customName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<List<UserContactItem>> GetContactsAsync(int ownerUserId)
        {
            var contacts = new List<UserContactItem>();
            using var conn = await _db.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT u.UserID, u.Username, COALESCE(c.CustomName, u.DisplayName) AS DisplayName,
                       u.AvatarPath, u.LastSeenAt
                FROM USER_CONTACTS c
                INNER JOIN USERS u ON c.ContactUserID = u.UserID
                WHERE c.OwnerUserID = @o AND u.IsActive = 1
                ORDER BY DisplayName ASC;
            ";
            cmd.Parameters.AddWithValue("@o", ownerUserId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                contacts.Add(new UserContactItem
                {
                    UserID = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    DisplayName = reader.GetString(2),
                    AvatarPath = reader.IsDBNull(3) ? null : reader.GetString(3),
                    LastSeenAt = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4))
                });
            }

            return contacts;
        }
    }
}
