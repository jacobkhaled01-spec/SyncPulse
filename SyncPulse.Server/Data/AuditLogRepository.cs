using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace SyncPulse.Server.Data
{
    public class AuditLogEntry
    {
        public int LogID { get; set; }
        public int? UserID { get; set; }
        public string LogLevel { get; set; } = "Info";
        public string Source { get; set; } = "Server";
        public string Message { get; set; } = string.Empty;
        public string? ClientIP { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AuditLogRepository
    {
        private readonly DatabaseManager _db;

        public AuditLogRepository(DatabaseManager db)
        {
            _db = db;
        }

        public async Task LogAsync(string level, string source, string message, int? userId = null, string? clientIp = null)
        {
            try
            {
                using var conn = await _db.CreateConnectionAsync();
                using var cmd = conn.CreateCommand();

                cmd.CommandText = @"
                    INSERT INTO SERVER_AUDIT_LOGS (UserID, LogLevel, Source, Message, ClientIP, CreatedAt)
                    VALUES (@u, @l, @s, @m, @ip, @now);
                ";
                cmd.Parameters.AddWithValue("@u", (object?)userId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@l", level);
                cmd.Parameters.AddWithValue("@s", source);
                cmd.Parameters.AddWithValue("@m", message);
                cmd.Parameters.AddWithValue("@ip", (object?)clientIp ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));

                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Fallback silently if logging fails to prevent system crash
            }
        }

        public async Task<List<AuditLogEntry>> GetRecentLogsAsync(int limit = 100)
        {
            var logs = new List<AuditLogEntry>();
            using var conn = await _db.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT LogID, UserID, LogLevel, Source, Message, ClientIP, CreatedAt
                FROM SERVER_AUDIT_LOGS
                ORDER BY LogID DESC
                LIMIT @lim;
            ";
            cmd.Parameters.AddWithValue("@lim", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                logs.Add(new AuditLogEntry
                {
                    LogID = reader.GetInt32(0),
                    UserID = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    LogLevel = reader.GetString(2),
                    Source = reader.GetString(3),
                    Message = reader.GetString(4),
                    ClientIP = reader.IsDBNull(5) ? null : reader.GetString(5),
                    CreatedAt = DateTime.Parse(reader.GetString(6))
                });
            }

            return logs;
        }
    }
}
