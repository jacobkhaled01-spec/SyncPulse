using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SyncPulse.Core.Enums;
using SyncPulse.Core.Packets;

namespace SyncPulse.Server.Data
{
    public class MessageRepository
    {
        private readonly DatabaseManager _db;

        public MessageRepository(DatabaseManager db)
        {
            _db = db;
        }

        /// <summary>
        /// جلب أو إنشاء معرف المحادثة الثنائية بين طرفين (User1_ID < User2_ID)
        /// </summary>
        public async Task<int> GetOrCreateConversationAsync(int userA, int userB)
        {
            int minUser = Math.Min(userA, userB);
            int maxUser = Math.Max(userA, userB);

            using var conn = await _db.CreateConnectionAsync();
            
            // 1. البحث عن المحادثة
            using var findCmd = conn.CreateCommand();
            findCmd.CommandText = "SELECT ConversationID FROM DIRECT_CONVERSATIONS WHERE User1_ID = @u1 AND User2_ID = @u2";
            findCmd.Parameters.AddWithValue("@u1", minUser);
            findCmd.Parameters.AddWithValue("@u2", maxUser);

            var result = await findCmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                return Convert.ToInt32(result);
            }

            // 2. إنشاء المحادثة لأول مرة
            string now = DateTime.UtcNow.ToString("o");
            using var createCmd = conn.CreateCommand();
            createCmd.CommandText = @"
                INSERT INTO DIRECT_CONVERSATIONS (User1_ID, User2_ID, LastActivityAt, CreatedAt)
                VALUES (@u1, @u2, @now, @now);
                SELECT last_insert_rowid();
            ";
            createCmd.Parameters.AddWithValue("@u1", minUser);
            createCmd.Parameters.AddWithValue("@u2", maxUser);
            createCmd.Parameters.AddWithValue("@now", now);

            return Convert.ToInt32(await createCmd.ExecuteScalarAsync());
        }

        /// <summary>
        /// حفظ الرسالة النصية في قاعدة البيانات وتحديث نشاط المحادثة
        /// </summary>
        public async Task<int> SaveMessageAsync(int conversationId, int senderId, int receiverId, string content, string? attachmentPath)
        {
            using var conn = await _db.CreateConnectionAsync();
            string now = DateTime.UtcNow.ToString("o");

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO MESSAGES (ConversationID, SenderID, ReceiverID, Content, AttachmentPath, Status, Timestamp)
                VALUES (@cid, @sid, @rid, @cnt, @att, 0, @now);
                
                UPDATE DIRECT_CONVERSATIONS SET LastActivityAt = @now WHERE ConversationID = @cid;
                
                SELECT last_insert_rowid();
            ";
            cmd.Parameters.AddWithValue("@cid", conversationId);
            cmd.Parameters.AddWithValue("@sid", senderId);
            cmd.Parameters.AddWithValue("@rid", receiverId);
            cmd.Parameters.AddWithValue("@cnt", content);
            cmd.Parameters.AddWithValue("@att", (object?)attachmentPath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@now", now);

            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        /// <summary>
        /// جلب كافة الرسائل المعلقة (غير المستلمة: Status = 0) لمستخدم انقطع اتصاله (Telegram Sync Engine)
        /// </summary>
        public async Task<List<ChatMessagePacket>> GetUndeliveredMessagesAsync(int receiverId)
        {
            var messages = new List<ChatMessagePacket>();
            using var conn = await _db.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT m.MessageID, m.ConversationID, m.SenderID, u.Username, m.ReceiverID,
                       m.Content, m.AttachmentPath, m.Status, m.Timestamp
                FROM MESSAGES m
                INNER JOIN USERS u ON m.SenderID = u.UserID
                WHERE m.ReceiverID = @rid AND m.Status = 0
                ORDER BY m.MessageID ASC;
            ";
            cmd.Parameters.AddWithValue("@rid", receiverId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                messages.Add(new ChatMessagePacket
                {
                    MessageID = reader.GetInt32(0),
                    ConversationID = reader.GetInt32(1),
                    SenderID = reader.GetInt32(2),
                    SenderUsername = reader.GetString(3),
                    ReceiverID = reader.GetInt32(4),
                    Content = reader.GetString(5),
                    AttachmentPath = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Status = (MessageStatus)reader.GetInt32(7),
                    Timestamp = DateTime.Parse(reader.GetString(8))
                });
            }

            return messages;
        }

        /// <summary>
        /// تحديث حالة الرسالة (Delivered = 1 / Read = 2)
        /// </summary>
        public async Task UpdateMessageStatusAsync(int messageId, MessageStatus newStatus)
        {
            using var conn = await _db.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE MESSAGES SET Status = @st WHERE MessageID = @id";
            cmd.Parameters.AddWithValue("@st", (int)newStatus);
            cmd.Parameters.AddWithValue("@id", messageId);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// جلب سجل المحادثة وتاريخ الرسائل السابقة
        /// </summary>
        public async Task<List<ChatMessagePacket>> GetConversationHistoryAsync(int conversationId, int limit = 50)
        {
            var history = new List<ChatMessagePacket>();
            using var conn = await _db.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT m.MessageID, m.ConversationID, m.SenderID, u.Username, m.ReceiverID,
                       m.Content, m.AttachmentPath, m.Status, m.Timestamp
                FROM MESSAGES m
                INNER JOIN USERS u ON m.SenderID = u.UserID
                WHERE m.ConversationID = @cid
                ORDER BY m.Timestamp ASC
                LIMIT @lim;
            ";
            cmd.Parameters.AddWithValue("@cid", conversationId);
            cmd.Parameters.AddWithValue("@lim", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                history.Add(new ChatMessagePacket
                {
                    MessageID = reader.GetInt32(0),
                    ConversationID = reader.GetInt32(1),
                    SenderID = reader.GetInt32(2),
                    SenderUsername = reader.GetString(3),
                    ReceiverID = reader.GetInt32(4),
                    Content = reader.GetString(5),
                    AttachmentPath = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Status = (MessageStatus)reader.GetInt32(7),
                    Timestamp = DateTime.Parse(reader.GetString(8))
                });
            }

            return history;
        }

        public async Task<int> GetTotalMessagesCountAsync()
        {
            using var conn = await _db.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM MESSAGES";
            return Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
        }
    }
}
