using System;
using System.Buffers.Binary;
using SyncPulse.Core.Enums;

namespace SyncPulse.Core.Protocol
{
    /// <summary>
    /// ترويسة تأطير حزم الشبكة القياسية (Fixed 12-Byte Frame Header - IETF TLV / Big-Endian)
    /// </summary>
    public readonly struct FrameHeader
    {
        public const byte ProtocolMagic = 0x53; // 'S' for SecureTalk / SyncPulse
        public const byte ProtocolVersion = 0x01;
        public const int HeaderSize = 12; // 12 Bytes fixed
        public const uint MaxPayloadSize = 10 * 1024 * 1024; // 10 MB limit for security

        public byte Magic { get; }
        public byte Version { get; }
        public PacketType Type { get; }
        public uint PayloadLength { get; }
        public uint SequenceNumber { get; }

        public FrameHeader(PacketType type, uint payloadLength, uint sequenceNumber = 0, byte version = ProtocolVersion)
        {
            if (payloadLength > MaxPayloadSize)
                throw new ArgumentOutOfRangeException(nameof(payloadLength), $"Payload exceeds maximum allowed size of {MaxPayloadSize} bytes.");

            Magic = ProtocolMagic;
            Version = version;
            Type = type;
            PayloadLength = payloadLength;
            SequenceNumber = sequenceNumber;
        }

        private FrameHeader(byte magic, byte version, PacketType type, uint payloadLength, uint sequenceNumber)
        {
            Magic = magic;
            Version = version;
            Type = type;
            PayloadLength = payloadLength;
            SequenceNumber = sequenceNumber;
        }

        /// <summary>
        /// تشفير الترويسة إلى مصفوفة 12 بايت بترتيب Big-Endian (Network Byte Order)
        /// </summary>
        public byte[] Serialize()
        {
            byte[] buffer = new byte[HeaderSize];
            Serialize(buffer.AsSpan());
            return buffer;
        }

        public void Serialize(Span<byte> destination)
        {
            if (destination.Length < HeaderSize)
                throw new ArgumentException($"Destination span must be at least {HeaderSize} bytes.", nameof(destination));

            destination[0] = Magic;
            destination[1] = Version;
            BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(2, 2), (ushort)Type);
            BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(4, 4), PayloadLength);
            BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(8, 4), SequenceNumber);
        }

        /// <summary>
        /// فك تشفير الترويسة والتحقق من صحتها من 12 بايت
        /// </summary>
        public static bool TryDeserialize(ReadOnlySpan<byte> source, out FrameHeader header, out string? errorMessage)
        {
            header = default;
            errorMessage = null;

            if (source.Length < HeaderSize)
            {
                errorMessage = "Insufficient bytes for header.";
                return false;
            }

            byte magic = source[0];
            if (magic != ProtocolMagic)
            {
                errorMessage = $"Invalid Magic Byte: 0x{magic:X2}, expected 0x{ProtocolMagic:X2}.";
                return false;
            }

            byte version = source[1];
            ushort rawType = BinaryPrimitives.ReadUInt16BigEndian(source.Slice(2, 2));
            uint length = BinaryPrimitives.ReadUInt32BigEndian(source.Slice(4, 4));
            uint seq = BinaryPrimitives.ReadUInt32BigEndian(source.Slice(8, 4));

            if (length > MaxPayloadSize)
            {
                errorMessage = $"Payload length ({length} bytes) exceeds maximum limit ({MaxPayloadSize} bytes).";
                return false;
            }

            header = new FrameHeader(magic, version, (PacketType)rawType, length, seq);
            return true;
        }

        public static FrameHeader Deserialize(ReadOnlySpan<byte> source)
        {
            if (!TryDeserialize(source, out FrameHeader header, out string? error))
                throw new InvalidOperationException(error);

            return header;
        }
    }
}
