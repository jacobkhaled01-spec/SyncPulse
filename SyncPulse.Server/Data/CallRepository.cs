using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SyncPulse.Core.Enums;
using SyncPulse.Core.Packets;

namespace SyncPulse.Server.Data
{
    public class CallRecordItem
    {
        public int CallID { get; set; }
        public int CallerID { get; set; }
        public string CallerUsername { get; set; } = string.Empty;
        public int ReceiverID { get; set; }
        public string ReceiverUsername { get; set; } = string.Empty;
        public string CallType { get; set; } = "Audio";
        public int DurationSeconds { get; set; }
        public string EndReason { get; set; } = "Completed";
        public DateTime StartedAt { get; set; }
    }

    public class CallRepository
    {
        private readonly DatabaseManager _db;

        public CallRepository(DatabaseManager db)
        {
            _db = db;
        }

        public async Task<int> LogCallStartAsync(int callerId, int receiverId, CallType callType)
        {
            using var conn = await _db.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO CALL_RECORDS (CallerID, ReceiverID, CallType, DurationSeconds, EndReason, StartedAt)
                VALUES (@c, @r, @t, 0, 'InProgress', @now);
                SELECT last_insert_rowid();
            ";
            cmd.Parameters.AddWithValue("@c", callerId);
            cmd.Parameters.AddWithValue("@r", receiverId);
            cmd.Parameters.AddWithValue("@t", callType.ToString());
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));

            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task EndCallAsync(int callId, int durationSeconds, string endReason)
        {
            using var conn = await _db.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE CALL_RECORDS
                SET DurationSeconds = @d, EndReason = @r
                WHERE CallID = @id;
            ";
            cmd.Parameters.AddWithValue("@d", durationSeconds);
            cmd.Parameters.AddWithValue("@r", endReason);
            cmd.Parameters.AddWithValue("@id", callId);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<CallRecordItem>> GetRecentCallsAsync(int limit = 50)
        {
            var list = new List<CallRecordItem>();
            using var conn = await _db.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT c.CallID, c.CallerID, u1.Username, c.ReceiverID, u2.Username,
                       c.CallType, c.DurationSeconds, c.EndReason, c.StartedAt
                FROM CALL_RECORDS c
                LEFT JOIN USERS u1 ON c.CallerID = u1.UserID
                LEFT JOIN USERS u2 ON c.ReceiverID = u2.UserID
                ORDER BY c.CallID DESC
                LIMIT @lim;
            ";
            cmd.Parameters.AddWithValue("@lim", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new CallRecordItem
                {
                    CallID = reader.GetInt32(0),
                    CallerID = reader.GetInt32(1),
                    CallerUsername = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2),
                    ReceiverID = reader.GetInt32(3),
                    ReceiverUsername = reader.IsDBNull(4) ? "Unknown" : reader.GetString(4),
                    CallType = reader.GetString(5),
                    DurationSeconds = reader.GetInt32(6),
                    EndReason = reader.GetString(7),
                    StartedAt = DateTime.Parse(reader.GetString(8))
                });
            }

            return list;
        }

        public async Task<List<CallHistoryItem>> GetUserCallHistoryAsync(int userId, int limit = 50)
        {
            var list = new List<CallHistoryItem>();
            using var conn = await _db.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT c.CallID, c.CallerID, COALESCE(u1.DisplayName, u1.Username, 'Unknown') AS CallerName,
                       c.ReceiverID, COALESCE(u2.DisplayName, u2.Username, 'Unknown') AS ReceiverName,
                       c.CallType, c.DurationSeconds, c.EndReason, c.StartedAt
                FROM CALL_RECORDS c
                LEFT JOIN USERS u1 ON c.CallerID = u1.UserID
                LEFT JOIN USERS u2 ON c.ReceiverID = u2.UserID
                WHERE c.CallerID = @uid OR c.ReceiverID = @uid
                ORDER BY c.CallID DESC
                LIMIT @lim;
            ";
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@lim", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int callerId = reader.GetInt32(1);
                string typeStr = reader.GetString(5);
                Enum.TryParse<CallType>(typeStr, out var type);

                list.Add(new CallHistoryItem
                {
                    CallID = reader.GetInt32(0),
                    CallerID = callerId,
                    CallerName = reader.GetString(2),
                    ReceiverID = reader.GetInt32(3),
                    ReceiverName = reader.GetString(4),
                    CallType = type,
                    DurationSeconds = reader.GetInt32(6),
                    Status = reader.GetString(7),
                    Timestamp = DateTime.Parse(reader.GetString(8)),
                    IsOutgoing = callerId == userId
                });
            }

            return list;
        }

        public async Task<int> GetTotalCallsCountAsync()
        {
            using var conn = await _db.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM CALL_RECORDS";
            return Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
        }
    }
}
