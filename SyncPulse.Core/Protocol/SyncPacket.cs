using System;
using SyncPulse.Core.Enums;
using SyncPulse.Core.Utils;

namespace SyncPulse.Core.Protocol
{
    /// <summary>
    /// الحزمة المتكاملة لنظام SecureTalk (Header + Payload)
    /// </summary>
    public class SyncPacket
    {
        public FrameHeader Header { get; }
        public byte[] Payload { get; }

        public SyncPacket(FrameHeader header, byte[] payload)
        {
            Header = header;
            Payload = payload ?? Array.Empty<byte>();
        }

        public SyncPacket(PacketType type, byte[] payload, uint sequenceNumber = 0)
        {
            Payload = payload ?? Array.Empty<byte>();
            Header = new FrameHeader(type, (uint)Payload.Length, sequenceNumber);
        }

        /// <summary>
        /// إنشاء حزمة من كائن DTO معين
        /// </summary>
        public static SyncPacket Create<T>(PacketType type, T payloadObject, uint sequenceNumber = 0)
        {
            byte[] payloadBytes = SerializationUtils.SerializeToUtf8Bytes(payloadObject);
            return new SyncPacket(type, payloadBytes, sequenceNumber);
        }

        /// <summary>
        /// قراءة كائن DTO من حمولة الحزمة
        /// </summary>
        public T? GetPayload<T>()
        {
            if (Payload.Length == 0) return default;
            return SerializationUtils.DeserializeFromUtf8Bytes<T>(Payload);
        }

        /// <summary>
        /// تشفير الحزمة كاملة (12 بايت ترويسة + N بايت حمولة) لإرسالها عبر المقبس
        /// </summary>
        public byte[] ToBytes()
        {
            byte[] buffer = new byte[FrameHeader.HeaderSize + Payload.Length];
            Header.Serialize(buffer.AsSpan(0, FrameHeader.HeaderSize));
            if (Payload.Length > 0)
            {
                Buffer.BlockCopy(Payload, 0, buffer, FrameHeader.HeaderSize, Payload.Length);
            }
            return buffer;
        }
    }
}
