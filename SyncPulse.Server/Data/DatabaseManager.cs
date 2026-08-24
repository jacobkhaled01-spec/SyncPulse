using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace SyncPulse.Server.Data
{
    /// <summary>
    /// مدير قاعدة البيانات المركزية ومسؤول التهيئة والجداول والفهارس (3NF Schema Manager)
    /// </summary>
    public class DatabaseManager
    {
        private readonly string _connectionString;
        public string DatabasePath { get; }

        public DatabaseManager(string dbFileName = "syncpulse_server.db")
        {
            string appData = AppDomain.CurrentDomain.BaseDirectory;
            DatabasePath = Path.Combine(appData, dbFileName);
            _connectionString = $"Data Source={DatabasePath};Mode=ReadWriteCreate;";
        }

        public SqliteConnection CreateConnection()
        {
            var conn = new SqliteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        public async Task<SqliteConnection> CreateConnectionAsync()
        {
            var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
        }

        /// <summary>
        /// تهيئة قاعدة البيانات وإنشاء الجداول السبعة والفهارس المعيارية
        /// </summary>
        public async Task InitializeDatabaseAsync()
        {
            using var conn = await CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                PRAGMA journal_mode = WAL;
                PRAGMA foreign_keys = ON;

                -- 1. جدول المستخدمين (USERS)
                CREATE TABLE IF NOT EXISTS USERS (
                    UserID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    DisplayName TEXT NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    Salt TEXT NOT NULL,
                    AvatarPath TEXT,
                    LastSeenAt TEXT,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    CreatedAt TEXT NOT NULL
                );

                -- 2. جدول جهات الاتصال (USER_CONTACTS)
                CREATE TABLE IF NOT EXISTS USER_CONTACTS (
                    OwnerUserID INTEGER NOT NULL,
                    ContactUserID INTEGER NOT NULL,
                    CustomName TEXT,
                    AddedAt TEXT NOT NULL,
                    PRIMARY KEY (OwnerUserID, ContactUserID),
                    FOREIGN KEY (OwnerUserID) REFERENCES USERS(UserID) ON DELETE CASCADE,
                    FOREIGN KEY (ContactUserID) REFERENCES USERS(UserID) ON DELETE CASCADE
                );

                -- 3. جدول الجلسات (USER_SESSIONS)
                CREATE TABLE IF NOT EXISTS USER_SESSIONS (
                    SessionToken TEXT PRIMARY KEY,
                    UserID INTEGER NOT NULL,
                    ClientIP TEXT,
                    DeviceInfo TEXT,
                    LoggedInAt TEXT NOT NULL,
                    IsOnline INTEGER NOT NULL DEFAULT 1,
                    FOREIGN KEY (UserID) REFERENCES USERS(UserID) ON DELETE CASCADE
                );

                -- 4. جدول المحادثات الثنائية (DIRECT_CONVERSATIONS)
                CREATE TABLE IF NOT EXISTS DIRECT_CONVERSATIONS (
                    ConversationID INTEGER PRIMARY KEY AUTOINCREMENT,
                    User1_ID INTEGER NOT NULL,
                    User2_ID INTEGER NOT NULL,
                    LastActivityAt TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY (User1_ID) REFERENCES USERS(UserID) ON DELETE CASCADE,
                    FOREIGN KEY (User2_ID) REFERENCES USERS(UserID) ON DELETE CASCADE
                );

                -- 5. جدول الرسائل (MESSAGES)
                CREATE TABLE IF NOT EXISTS MESSAGES (
                    MessageID INTEGER PRIMARY KEY AUTOINCREMENT,
                    ConversationID INTEGER NOT NULL,
                    SenderID INTEGER NOT NULL,
                    ReceiverID INTEGER NOT NULL,
                    Content TEXT NOT NULL,
                    AttachmentPath TEXT,
                    Status INTEGER NOT NULL DEFAULT 0, -- 0: Sent(✓), 1: Delivered(✓✓), 2: Read
                    Timestamp TEXT NOT NULL,
                    FOREIGN KEY (ConversationID) REFERENCES DIRECT_CONVERSATIONS(ConversationID) ON DELETE CASCADE,
                    FOREIGN KEY (SenderID) REFERENCES USERS(UserID) ON DELETE CASCADE,
                    FOREIGN KEY (ReceiverID) REFERENCES USERS(UserID) ON DELETE CASCADE
                );

                -- 6. جدول سجلات المكالمات (CALL_RECORDS)
                CREATE TABLE IF NOT EXISTS CALL_RECORDS (
                    CallID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CallerID INTEGER NOT NULL,
                    ReceiverID INTEGER NOT NULL,
                    CallType TEXT NOT NULL,
                    DurationSeconds INTEGER NOT NULL DEFAULT 0,
                    EndReason TEXT NOT NULL,
                    StartedAt TEXT NOT NULL,
                    FOREIGN KEY (CallerID) REFERENCES USERS(UserID) ON DELETE CASCADE,
                    FOREIGN KEY (ReceiverID) REFERENCES USERS(UserID) ON DELETE CASCADE
                );

                -- 7. جدول سجلات تدقيق الخادم (SERVER_AUDIT_LOGS)
                CREATE TABLE IF NOT EXISTS SERVER_AUDIT_LOGS (
                    LogID INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserID INTEGER,
                    LogLevel TEXT NOT NULL,
                    Source TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    ClientIP TEXT,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY (UserID) REFERENCES USERS(UserID) ON DELETE SET NULL
                );

                -- الفهارس عالية الأداء (Smart High-Performance Indexes)
                CREATE INDEX IF NOT EXISTS idx_offline_messages ON MESSAGES(ReceiverID, Status);
                CREATE INDEX IF NOT EXISTS idx_conversation_history ON MESSAGES(ConversationID, Timestamp DESC);
                CREATE UNIQUE INDEX IF NOT EXISTS idx_unique_direct_chat ON DIRECT_CONVERSATIONS(User1_ID, User2_ID);
                CREATE INDEX IF NOT EXISTS idx_active_sessions ON USER_SESSIONS(UserID, IsOnline);
            ";

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
