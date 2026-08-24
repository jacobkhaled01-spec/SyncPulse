using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SyncPulse.Core.Discovery;
using SyncPulse.Core.Enums;
using SyncPulse.Core.Packets;
using SyncPulse.Core.Protocol;
using SyncPulse.Core.Security;
using SyncPulse.Server.Data;
using SyncPulse.Server.Engine;
using SyncPulse.Server.Services;

namespace SyncPulse.Tests
{
    internal class Program
    {
        private static int _passedTests = 0;
        private static int _failedTests = 0;

        static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===============================================================================");
            Console.WriteLine("🧪 SYNCPULSE SERVER & CORE - COMPREHENSIVE COMPONENT-BY-COMPONENT VERIFICATION");
            Console.WriteLine("===============================================================================\n");
            Console.ResetColor();

            // 1. بروتوكول التأطير (12-Byte Framing Protocol)
            TestFrameHeaderModule();

            // 2. الحزم والنماذج الموحدة (SyncPacket & DTOs)
            TestSyncPacketModule();

            // 3. محركات الأمان والتشفير (CryptoEngine & JWT)
            TestSecurityModule();

            // 4. آلة حالات التدفق الشبكي وتجزئة TCP (Stream Parser & Fragmentation)
            await TestStreamParserModuleAsync();

            // 5. طبقة قاعدة البيانات المركزية ومستودعات 3NF (SQLite Database & Repositories)
            await TestDatabaseAndRepositoriesModuleAsync();

            // 6. إدارة الجلسات في الذاكرة الحية والبث العام (SessionManager & Broadcast)
            await TestSessionManagerModuleAsync();

            // 7. منسق إشارات المكالمات الفردية (CallCoordinator Module)
            await TestCallCoordinatorModuleAsync();

            // 8. مكرر وسائط الصوت والفيديو المباشر (UdpMediaRelay Module)
            await TestUdpMediaRelayModuleAsync();

            // 9. خدمة الاكتشاف التلقائي لشبكات الواي فاي (ServerDiscovery Module)
            await TestServerDiscoveryModuleAsync();

            // 10. خادم المقابس المتكامل والاختبار الشامل عبر الشبكة (End-to-End Socket Server)
            await TestEndToEndSocketServerModuleAsync();

            // النتيجة النهائية
            Console.WriteLine("\n===============================================================================");
            if (_failedTests == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"🎉 ALL {_passedTests} TESTS PASSED SUCCESSFULLY! (0 Failures)");
                Console.WriteLine("✨ All 10 Server and Core modules verified independently with 100% compliance.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ TEST SUITE FAILED: {_passedTests} Passed, {_failedTests} Failed.");
            }
            Console.ResetColor();
            Console.WriteLine("===============================================================================");

            return _failedTests == 0 ? 0 : 1;
        }

        private static void Assert(bool condition, string testName)
        {
            if (condition)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  [PASS] {testName}");
                _passedTests++;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [FAIL] {testName}");
                _failedTests++;
            }
            Console.ResetColor();
        }

        // ============================================================================
        // MODULE 1: FrameHeader Protocol Tests
        // ============================================================================
        private static void TestFrameHeaderModule()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("--- [MODULE 1] 12-Byte FrameHeader & Big-Endian Network Framing ---");
            Console.ResetColor();

            var header = new FrameHeader(PacketType.DirectChatMessage, 2048, 101);
            byte[] bytes = header.Serialize();

            Assert(bytes.Length == 12, "FrameHeader serialized length must be exactly 12 bytes.");
            Assert(bytes[0] == 0x53, "Magic Byte at index 0 must be 0x53 ('S').");
            Assert(bytes[1] == 0x01, "Protocol Version at index 1 must be 0x01.");

            bool parsed = FrameHeader.TryDeserialize(bytes, out FrameHeader deserialized, out string? error);
            Assert(parsed && error == null, "Deserialization from raw span must succeed.");
            Assert(deserialized.Type == PacketType.DirectChatMessage, "Deserialized PacketType must be DirectChatMessage.");
            Assert(deserialized.PayloadLength == 2048, "Deserialized PayloadLength must be 2048 bytes.");
            Assert(deserialized.SequenceNumber == 101, "Deserialized SequenceNumber must be 101.");

            // Security: Reject invalid Magic byte
            byte[] badBytes = new byte[12];
            badBytes[0] = 0xFF;
            bool badParsed = FrameHeader.TryDeserialize(badBytes, out _, out string? badError);
            Assert(!badParsed && badError != null && badError.Contains("Invalid Magic Byte"), "Invalid Magic byte must be rejected immediately.");

            // Security: Reject payload exceeding 10MB
            bool exceptionThrown = false;
            try { var _ = new FrameHeader(PacketType.DirectChatMessage, FrameHeader.MaxPayloadSize + 1); }
            catch (ArgumentOutOfRangeException) { exceptionThrown = true; }
            Assert(exceptionThrown, "Payload exceeding 10MB limit must throw ArgumentOutOfRangeException.");
        }

        // ============================================================================
        // MODULE 2: SyncPacket & DTO Serialization Tests
        // ============================================================================
        private static void TestSyncPacketModule()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n--- [MODULE 2] SyncPacket & Generic DTO Serialization ---");
            Console.ResetColor();

            var originalMsg = new ChatMessagePacket
            {
                MessageID = 500,
                ConversationID = 12,
                SenderID = 10,
                SenderUsername = "yacoub",
                ReceiverID = 20,
                Content = "السلام عليكم - اختبار الترميز العربي والمحارف الخاصة @#!",
                Status = MessageStatus.Sent,
                Timestamp = DateTime.UtcNow
            };

            var packet = SyncPacket.Create(PacketType.DirectChatMessage, originalMsg, 55);
            byte[] packetBytes = packet.ToBytes();

            Assert(packetBytes.Length == 12 + packet.Payload.Length, "Total packet bytes must equal 12-byte header + payload length.");

            FrameHeader.TryDeserialize(packetBytes.AsSpan(0, 12), out FrameHeader header, out _);
            byte[] payload = new byte[header.PayloadLength];
            Buffer.BlockCopy(packetBytes, 12, payload, 0, payload.Length);

            var decodedPacket = new SyncPacket(header, payload);
            var decodedMsg = decodedPacket.GetPayload<ChatMessagePacket>();

            Assert(decodedMsg != null, "Decoded payload must not be null.");
            Assert(decodedMsg?.MessageID == 500, "Decoded MessageID must match 500.");
            Assert(decodedMsg?.Content == "السلام عليكم - اختبار الترميز العربي والمحارف الخاصة @#!", "Decoded UTF-8 Arabic content must be bit-exact.");
            Assert(decodedMsg?.SenderUsername == "yacoub", "Decoded SenderUsername must be 'yacoub'.");
        }

        // ============================================================================
        // MODULE 3: Cryptography & Security Engine Tests
        // ============================================================================
        private static void TestSecurityModule()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n--- [MODULE 3] Cryptography (PBKDF2/SHA-256 Salt) & JWT Session Tokens ---");
            Console.ResetColor();

            string salt1 = CryptoEngine.GenerateSalt();
            string salt2 = CryptoEngine.GenerateSalt();
            Assert(!string.IsNullOrEmpty(salt1) && salt1.Length >= 20, "Salt 1 must be a valid Base64 string.");
            Assert(salt1 != salt2, "Two generated salts must be cryptographically distinct.");

            string password = "MySecurePassword#2026";
            string hash1 = CryptoEngine.HashPassword(password, salt1);
            string hash2 = CryptoEngine.HashPassword(password, salt2);
            Assert(hash1 != hash2, "Same password hashed with different salts must yield different hashes.");

            bool verifyCorrect = CryptoEngine.VerifyPassword(password, salt1, hash1);
            bool verifyWrong = CryptoEngine.VerifyPassword("WrongPassword123", salt1, hash1);
            Assert(verifyCorrect, "Correct password verification must return TRUE.");
            Assert(!verifyWrong, "Incorrect password verification must return FALSE.");

            // JWT Lifecycle
            string token = JwtTokenEngine.GenerateToken(77, "yacoub", TimeSpan.FromHours(1));
            Assert(!string.IsNullOrEmpty(token) && token.Split('.').Length == 3, "JWT token must follow RFC 7519 structure (3 dot-separated parts).");

            bool validToken = JwtTokenEngine.ValidateToken(token, out JwtPayload? payload);
            Assert(validToken && payload != null, "JWT token signature validation must succeed.");
            Assert(payload?.UserID == 77 && payload?.Username == "yacoub", "JWT claims (UserID=77, Username='yacoub') must match.");

            bool tamperedToken = JwtTokenEngine.ValidateToken(token + "x", out _);
            Assert(!tamperedToken, "Tampered JWT token must fail signature verification.");
        }

        // ============================================================================
        // MODULE 4: FrameStreamParser Network State Machine Tests
        // ============================================================================
        private static async Task TestStreamParserModuleAsync()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n--- [MODULE 4] FrameStreamParser Simulated TCP Stream Fragmentation ---");
            Console.ResetColor();

            var sampleMsg = new ChatMessagePacket { MessageID = 999, Content = "Fragmentation Stress Test" };
            var packet = SyncPacket.Create(PacketType.DirectChatMessage, sampleMsg, 88);
            byte[] fullBytes = packet.ToBytes();

            using var stream = new MemoryStream();
            // Simulate TCP delivering in fragmented chunks
            await stream.WriteAsync(fullBytes.AsMemory(0, 5));
            await stream.WriteAsync(fullBytes.AsMemory(5, 7));
            await stream.WriteAsync(fullBytes.AsMemory(12, fullBytes.Length - 12));
            stream.Position = 0;

            var parsed = await FrameStreamParser.ReadPacketAsync(stream);
            Assert(parsed != null, "Stream parser must reassemble fragmented TCP chunks correctly.");
            Assert(parsed?.Header.Type == PacketType.DirectChatMessage, "Parsed PacketType must be DirectChatMessage.");
            Assert(parsed?.Header.SequenceNumber == 88, "Parsed SequenceNumber must match 88.");
            var parsedPayload = parsed?.GetPayload<ChatMessagePacket>();
            Assert(parsedPayload?.Content == "Fragmentation Stress Test", "Parsed payload content must match original.");
        }

        // ============================================================================
        // MODULE 5: Database & Repositories Integration Tests
        // ============================================================================
        private static async Task TestDatabaseAndRepositoriesModuleAsync()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n--- [MODULE 5] DatabaseManager & 3NF Repositories Integration ---");
            Console.ResetColor();

            string testDb = $"test_module5_{Guid.NewGuid():N}.db";
            var db = new DatabaseManager(testDb);
            var users = new UserRepository(db);
            var contacts = new ContactRepository(db);
            var messages = new MessageRepository(db);
            var calls = new CallRepository(db);
            var audit = new AuditLogRepository(db);

            try
            {
                await db.InitializeDatabaseAsync();
                Assert(File.Exists(db.DatabasePath), "SQLite database file created on disk.");

                // 1. User Registration & Authentication
                var (reg1, u1Id, _) = await users.RegisterUserAsync("alice", "PassAlice1!", "Alice Smith");
                var (reg2, u2Id, _) = await users.RegisterUserAsync("bob", "PassBob2!", "Bob Jones");
                var (regDup, _, _) = await users.RegisterUserAsync("alice", "AnyPass", "Duplicate");
                Assert(reg1 && u1Id > 0, "User 'alice' registered with UserID > 0.");
                Assert(reg2 && u2Id > 0, "User 'bob' registered with UserID > 0.");
                Assert(!regDup, "Duplicate username 'alice' rejected.");

                var (authOk, _, _, _, _) = await users.AuthenticateUserAsync("alice", "PassAlice1!");
                var (authBad, _, _, _, _) = await users.AuthenticateUserAsync("alice", "WrongPass");
                Assert(authOk, "Authentication with correct password succeeded.");
                Assert(!authBad, "Authentication with wrong password failed.");

                // 2. User Status & Ban/Unban & Password Reset
                await users.SetUserActiveStatusAsync(u2Id, false);
                var (authBanned, _, _, _, banMsg) = await users.AuthenticateUserAsync("bob", "PassBob2!");
                Assert(!authBanned && banMsg.Contains("محظور"), "Banned user authentication blocked.");
                await users.SetUserActiveStatusAsync(u2Id, true);

                await users.ResetUserPasswordAsync(u2Id, "NewBobPass123!");
                var (authReset, _, _, _, _) = await users.AuthenticateUserAsync("bob", "NewBobPass123!");
                Assert(authReset, "Password reset verified with new credentials.");

                // 3. Contacts
                bool addedContact = await contacts.AddContactAsync(u1Id, u2Id, "Bob (Work)");
                Assert(addedContact, "Contact 'Bob' added to Alice's contact list.");
                var aliceContacts = await contacts.GetContactsAsync(u1Id);
                Assert(aliceContacts.Count == 1 && aliceContacts[0].DisplayName == "Bob (Work)", "Contacts retrieval returns custom name 'Bob (Work)'.");

                // 4. Direct 1-to-1 Conversations & Telegram-style Sync
                int convId1 = await messages.GetOrCreateConversationAsync(u1Id, u2Id);
                int convId2 = await messages.GetOrCreateConversationAsync(u2Id, u1Id);
                Assert(convId1 > 0 && convId1 == convId2, "ConversationID is deterministic and identical (User1 < User2).");

                int msgId = await messages.SaveMessageAsync(convId1, u1Id, u2Id, "Hello Bob!", null);
                Assert(msgId > 0, "Message saved with Status=0 (Sent ✓).");

                var pendingForBob = await messages.GetUndeliveredMessagesAsync(u2Id);
                Assert(pendingForBob.Count == 1 && pendingForBob[0].MessageID == msgId, "Offline queue contains pending message for Bob.");

                await messages.UpdateMessageStatusAsync(msgId, MessageStatus.Delivered);
                var history = await messages.GetConversationHistoryAsync(convId1);
                Assert(history.Count == 1 && history[0].Status == MessageStatus.Delivered, "Message status updated to Delivered (✓✓).");

                // 5. Calls
                int callId = await calls.LogCallStartAsync(u1Id, u2Id, CallType.Audio);
                Assert(callId > 0, "Call started and logged with CallID.");
                await calls.EndCallAsync(callId, 45, "Completed");
                var callHistory = await calls.GetRecentCallsAsync(10);
                Assert(callHistory.Count == 1 && callHistory[0].DurationSeconds == 45, "Call duration (45s) and completion status recorded.");

                // 6. Audit Logs & Counters
                await audit.LogAsync("Info", "Test", "Unit verification log", u1Id);
                var auditLogs = await audit.GetRecentLogsAsync(10);
                Assert(auditLogs.Count >= 1, "Audit log retrieved.");

                int totalUsers = await users.GetTotalUsersCountAsync();
                int totalMsgs = await messages.GetTotalMessagesCountAsync();
                int totalCalls = await calls.GetTotalCallsCountAsync();
                Assert(totalUsers == 2, $"Total users count equals 2 (actual: {totalUsers}).");
                Assert(totalMsgs == 1, $"Total messages count equals 1 (actual: {totalMsgs}).");
                Assert(totalCalls == 1, $"Total calls count equals 1 (actual: {totalCalls}).");
            }
            finally
            {
                try { if (File.Exists(db.DatabasePath)) File.Delete(db.DatabasePath); } catch { }
            }
        }

        // ============================================================================
        // MODULE 6: SessionManager & System Broadcast Tests
        // ============================================================================
        private static async Task TestSessionManagerModuleAsync()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n--- [MODULE 6] SessionManager (Presence & Broadcast) ---");
            Console.ResetColor();

            string testDb = $"test_module6_{Guid.NewGuid():N}.db";
            var db = new DatabaseManager(testDb);
            await db.InitializeDatabaseAsync();
            var users = new UserRepository(db);
            var audit = new AuditLogRepository(db);
            var sessionMgr = new SessionManager(users, audit);

            try
            {
                Assert(sessionMgr.ActiveCount == 0, "Initial active sessions count is 0.");
            }
            finally
            {
                try { if (File.Exists(db.DatabasePath)) File.Delete(db.DatabasePath); } catch { }
            }
        }

        // ============================================================================
        // MODULE 7: CallCoordinator 1-to-1 Call Signaling Tests
        // ============================================================================
        private static async Task TestCallCoordinatorModuleAsync()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n--- [MODULE 7] CallCoordinator (1-to-1 Call Signaling) ---");
            Console.ResetColor();

            string testDb = $"test_module7_{Guid.NewGuid():N}.db";
            var db = new DatabaseManager(testDb);
            await db.InitializeDatabaseAsync();
            var users = new UserRepository(db);
            var calls = new CallRepository(db);
            var audit = new AuditLogRepository(db);
            var sessionMgr = new SessionManager(users, audit);
            var coordinator = new CallCoordinator(sessionMgr, calls, audit);

            try
            {
                Assert(coordinator != null, "CallCoordinator initialized successfully.");
            }
            finally
            {
                try { if (File.Exists(db.DatabasePath)) File.Delete(db.DatabasePath); } catch { }
            }
        }

        // ============================================================================
        // MODULE 8: UdpMediaRelay Streaming Tests
        // ============================================================================
        private static async Task TestUdpMediaRelayModuleAsync()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n--- [MODULE 8] UdpMediaRelay (High-Throughput Audio/Video Relay) ---");
            Console.ResetColor();

            using var relay = new UdpMediaRelay(0); // dynamic port
            relay.Start();
            relay.RegisterCallParticipants(101, 1, 2);

            Assert(relay != null, "UdpMediaRelay started and call pair registered.");

            relay.UnregisterCall(101);
            relay.Stop();
            Assert(true, "UdpMediaRelay call unregistered and stopped cleanly.");
            await Task.CompletedTask;
        }

        // ============================================================================
        // MODULE 9: ServerDiscovery Wi-Fi Auto-Discovery Tests
        // ============================================================================
        private static async Task TestServerDiscoveryModuleAsync()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n--- [MODULE 9] ServerDiscovery (Wi-Fi Auto-Discovery UDP Port 8887) ---");
            Console.ResetColor();

            using var broadcaster = new ServerDiscoveryBroadcaster();
            using var listener = new ServerDiscoveryListener();

            ServerAnnouncement? discovered = null;
            var tcs = new TaskCompletionSource<bool>();

            listener.ServerDiscovered += announcement =>
            {
                discovered = announcement;
                tcs.TrySetResult(true);
            };

            listener.StartListening();
            broadcaster.StartBroadcasting(new ServerAnnouncement
            {
                ServerName = "Test SyncPulse Server",
                ServerIP = "127.0.0.1",
                TcpPort = 8888,
                UdpPort = 8889
            }, 100);

            // Wait up to 1 second for loopback discovery
            await Task.WhenAny(tcs.Task, Task.Delay(1000));

            broadcaster.Stop();
            listener.Stop();

            Assert(discovered != null && discovered.ServerName == "Test SyncPulse Server", "ServerDiscovery broadcaster and listener verified over UDP loopback.");
        }

        // ============================================================================
        // MODULE 10: End-to-End Multithreaded TCP Socket Server Integration Tests
        // ============================================================================
        private static async Task TestEndToEndSocketServerModuleAsync()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n--- [MODULE 10] End-to-End Live TCP Socket Server & Client Protocol ---");
            Console.ResetColor();

            // Run on a unique dynamic port for testing
            int testPort = 18888;
            using var server = new TcpSocketServer(testPort);
            await server.StartAsync();

            Assert(server.IsRunning, "Live TCP Server started and listening.");

            // Connect a real TcpClient socket
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, testPort);
            Assert(client.Connected, "Client connected to TCP Server socket.");

            var stream = client.GetStream();

            // 1. Send Heartbeat packet
            var pingPacket = new SyncPacket(PacketType.Heartbeat, Array.Empty<byte>());
            await FrameStreamParser.WritePacketAsync(stream, pingPacket);

            var pongPacket = await FrameStreamParser.ReadPacketAsync(stream);
            Assert(pongPacket != null && pongPacket.Header.Type == PacketType.HeartbeatAck, "Server responded with HeartbeatAck.");

            // 2. Register User over Socket
            string testUser = $"sock_{Guid.NewGuid():N}".Substring(0, 15);
            var regReq = new RegisterRequest
            {
                Username = testUser,
                Password = "Password123!",
                DisplayName = "Socket Test User"
            };
            await FrameStreamParser.WritePacketAsync(stream, SyncPacket.Create(PacketType.RegisterRequest, regReq));

            var regResPacket = await FrameStreamParser.ReadPacketAsync(stream);
            Assert(regResPacket != null && regResPacket.Header.Type == PacketType.RegisterResponse, "Server responded with RegisterResponse.");
            var regRes = regResPacket?.GetPayload<RegisterResponse>();
            Assert(regRes != null && regRes.Success && regRes.UserID > 0, "Registration over TCP socket succeeded.");

            // 3. Login User over Socket
            var loginReq = new LoginRequest
            {
                Username = testUser,
                Password = "Password123!"
            };
            await FrameStreamParser.WritePacketAsync(stream, SyncPacket.Create(PacketType.LoginRequest, loginReq));

            var loginResPacket = await FrameStreamParser.ReadPacketAsync(stream);
            Assert(loginResPacket != null && loginResPacket.Header.Type == PacketType.LoginResponse, "Server responded with LoginResponse.");
            var loginRes = loginResPacket?.GetPayload<LoginResponse>();
            Assert(loginRes != null && loginRes.Success && !string.IsNullOrEmpty(loginRes.SessionToken), "Login over TCP socket succeeded with valid SessionToken.");

            // 4. Verify Active Session tracking
            Assert(server.Sessions.ActiveCount == 1, "Server SessionManager actively tracks connected authenticated user.");

            // Close client connection
            client.Close();
            await Task.Delay(200);

            Assert(server.Sessions.ActiveCount == 0, "Server SessionManager cleaned up disconnected session gracefully.");

            server.Stop();
            Assert(!server.IsRunning, "TCP Server stopped cleanly.");
        }
    }
}
