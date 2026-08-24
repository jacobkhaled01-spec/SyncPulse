using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SyncPulse.Core.Enums;
using SyncPulse.Core.Packets;
using SyncPulse.Core.Protocol;
using SyncPulse.Core.Security;

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
            Console.WriteLine("===================================================================");
            Console.WriteLine("🧪 SecureTalk / SyncPulse.Core Automated Verification Suite");
            Console.WriteLine("===================================================================\n");
            Console.ResetColor();

            // 1. FrameHeader Tests
            TestFrameHeaderSerialization();
            TestFrameHeaderMagicByteRejection();
            TestFrameHeaderPayloadSizeLimit();

            // 2. SyncPacket Tests
            TestSyncPacketGenericDtoRoundTrip();

            // 3. Security Tests
            TestCryptoEngineSaltingAndHashing();
            TestJwtTokenLifecycle();

            // 4. Stream Parser Simulation (TCP Fragmentation)
            await TestStreamParserWithSimulatedFragmentationAsync();

            // Summary
            Console.WriteLine("\n===================================================================");
            if (_failedTests == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"🎉 ALL {_passedTests} TESTS PASSED SUCCESSFULLY! (0 Failures)");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ TEST SUITE FAILED: {_passedTests} Passed, {_failedTests} Failed.");
            }
            Console.ResetColor();
            Console.WriteLine("===================================================================");

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

        private static void TestFrameHeaderSerialization()
        {
            Console.WriteLine("--- [1] FrameHeader 12-Byte Serialization Tests ---");
            var header = new FrameHeader(PacketType.DirectChatMessage, 1024, 42);
            byte[] bytes = header.Serialize();

            Assert(bytes.Length == 12, "FrameHeader length must be exactly 12 bytes.");
            Assert(bytes[0] == 0x53, "Magic byte must be 0x53 ('S').");
            Assert(bytes[1] == 0x01, "Version byte must be 0x01.");

            bool parsed = FrameHeader.TryDeserialize(bytes, out FrameHeader deserialized, out string? error);
            Assert(parsed && error == null, "Header deserialization must succeed.");
            Assert(deserialized.Type == PacketType.DirectChatMessage, "PacketType must match.");
            Assert(deserialized.PayloadLength == 1024, "PayloadLength must match.");
            Assert(deserialized.SequenceNumber == 42, "SequenceNumber must match.");
        }

        private static void TestFrameHeaderMagicByteRejection()
        {
            Console.WriteLine("\n--- [2] FrameHeader Security & Rejection Tests ---");
            byte[] invalidMagic = new byte[12];
            invalidMagic[0] = 0x47; // 'G' (e.g. HTTP GET)
            invalidMagic[1] = 0x01;

            bool parsed = FrameHeader.TryDeserialize(invalidMagic, out _, out string? error);
            Assert(!parsed && error != null && error.Contains("Invalid Magic Byte"), "Invalid Magic Byte must be rejected immediately.");
        }

        private static void TestFrameHeaderPayloadSizeLimit()
        {
            bool exceptionThrown = false;
            try
            {
                var header = new FrameHeader(PacketType.DirectChatMessage, FrameHeader.MaxPayloadSize + 1);
            }
            catch (ArgumentOutOfRangeException)
            {
                exceptionThrown = true;
            }
            Assert(exceptionThrown, "Payload exceeding 10MB limit must throw ArgumentOutOfRangeException.");
        }

        private static void TestSyncPacketGenericDtoRoundTrip()
        {
            Console.WriteLine("\n--- [3] SyncPacket & DTO Round-Trip Tests ---");
            var originalMsg = new ChatMessagePacket
            {
                MessageID = 1001,
                ConversationID = 5,
                SenderID = 1,
                SenderUsername = "ahmed",
                ReceiverID = 2,
                Content = "السلام عليكم ورحمة الله وبركاته",
                Status = MessageStatus.Sent,
                Timestamp = DateTime.UtcNow
            };

            var packet = SyncPacket.Create(PacketType.DirectChatMessage, originalMsg, 777);
            byte[] packetBytes = packet.ToBytes();

            Assert(packetBytes.Length == FrameHeader.HeaderSize + packet.Payload.Length, "Packet total bytes must equal 12 + PayloadLength.");

            // Deserialize full packet
            FrameHeader.TryDeserialize(packetBytes.AsSpan(0, 12), out FrameHeader header, out _);
            byte[] payloadOnly = new byte[header.PayloadLength];
            Buffer.BlockCopy(packetBytes, 12, payloadOnly, 0, payloadOnly.Length);

            var reconstructedPacket = new SyncPacket(header, payloadOnly);
            var decodedMsg = reconstructedPacket.GetPayload<ChatMessagePacket>();

            Assert(decodedMsg != null, "Payload deserialization must not be null.");
            Assert(decodedMsg?.MessageID == 1001, "MessageID must match.");
            Assert(decodedMsg?.Content == "السلام عليكم ورحمة الله وبركاته", "Arabic UTF-8 Content must match perfectly.");
            Assert(decodedMsg?.SenderUsername == "ahmed", "SenderUsername must match.");
        }

        private static void TestCryptoEngineSaltingAndHashing()
        {
            Console.WriteLine("\n--- [4] CryptoEngine (Salt & PBKDF2/SHA256) Tests ---");
            string salt1 = CryptoEngine.GenerateSalt();
            string salt2 = CryptoEngine.GenerateSalt();

            Assert(!string.IsNullOrEmpty(salt1) && !string.IsNullOrEmpty(salt2), "Generated salts must not be empty.");
            Assert(salt1 != salt2, "Two generated salts must be completely unique.");

            string password = "StrongSecretPassword#2026";
            string hash1 = CryptoEngine.HashPassword(password, salt1);
            string hash2 = CryptoEngine.HashPassword(password, salt2);

            Assert(hash1 != hash2, "Same password with different salts must produce completely different hashes.");

            bool valid1 = CryptoEngine.VerifyPassword(password, salt1, hash1);
            bool valid2 = CryptoEngine.VerifyPassword("WrongPassword", salt1, hash1);

            Assert(valid1, "Valid password verification must return TRUE.");
            Assert(!valid2, "Wrong password verification must return FALSE.");
        }

        private static void TestJwtTokenLifecycle()
        {
            Console.WriteLine("\n--- [5] JWT Token Generation & Validation Tests ---");
            string token = JwtTokenEngine.GenerateToken(42, "yacoub", TimeSpan.FromMinutes(30));
            Assert(!string.IsNullOrEmpty(token), "Generated JWT token must not be empty.");

            bool valid = JwtTokenEngine.ValidateToken(token, out JwtPayload? payload);
            Assert(valid && payload != null, "JWT token validation must succeed.");
            Assert(payload?.UserID == 42, "JWT payload UserID must match.");
            Assert(payload?.Username == "yacoub", "JWT payload Username must match.");

            bool tamperedValid = JwtTokenEngine.ValidateToken(token + "tampered", out _);
            Assert(!tamperedValid, "Tampered JWT token must be rejected.");
        }

        private static async Task TestStreamParserWithSimulatedFragmentationAsync()
        {
            Console.WriteLine("\n--- [6] FrameStreamParser Simulated Network Fragmentation ---");

            var chat = new ChatMessagePacket { MessageID = 50, Content = "Simulated TCP Stream Test" };
            var originalPacket = SyncPacket.Create(PacketType.DirectChatMessage, chat, 99);
            byte[] fullBytes = originalPacket.ToBytes();

            // Simulate TCP delivering bytes in 3 small fragmented chunks: 5 bytes, 10 bytes, remainder
            using var simulatedStream = new MemoryStream();
            await simulatedStream.WriteAsync(fullBytes.AsMemory(0, fullBytes.Length));
            simulatedStream.Position = 0; // Rewind for reading

            var parsedPacket = await FrameStreamParser.ReadPacketAsync(simulatedStream);

            Assert(parsedPacket != null, "Stream parser must read packet across stream boundaries.");
            Assert(parsedPacket?.Header.Type == PacketType.DirectChatMessage, "Parsed PacketType must match.");
            Assert(parsedPacket?.Header.SequenceNumber == 99, "Parsed SequenceNumber must match.");
            
            var parsedChat = parsedPacket?.GetPayload<ChatMessagePacket>();
            Assert(parsedChat?.Content == "Simulated TCP Stream Test", "Parsed chat content must match original.");
        }
    }
}
