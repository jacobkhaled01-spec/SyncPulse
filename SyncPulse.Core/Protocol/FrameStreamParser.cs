using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SyncPulse.Core.Protocol
{
    /// <summary>
    /// قارئ تدفق المقابس ومعالج تأطير الحزم غير المتزامن (Stream Framing State Machine)
    /// </summary>
    public static class FrameStreamParser
    {
        /// <summary>
        /// قراءة حزمة واحدة كاملة من التدفق مع معالجة تجزئة وتداخل حزم TCP
        /// </summary>
        public static async Task<SyncPacket?> ReadPacketAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            byte[] headerBuffer = new byte[FrameHeader.HeaderSize];
            
            // 1. قراءة الترويسة كاملة (12 بايت)
            if (!await ReadExactBytesAsync(stream, headerBuffer, 0, FrameHeader.HeaderSize, cancellationToken))
            {
                return null; // Connection closed gracefully
            }

            // 2. التحقق من الترويسة وفك تشفيرها
            if (!FrameHeader.TryDeserialize(headerBuffer, out FrameHeader header, out string? errorMessage))
            {
                throw new InvalidDataException($"Invalid packet header: {errorMessage}");
            }

            // 3. قراءة الحمولة (Payload) بحجم PayloadLength
            byte[] payloadBuffer = Array.Empty<byte>();
            if (header.PayloadLength > 0)
            {
                payloadBuffer = new byte[header.PayloadLength];
                if (!await ReadExactBytesAsync(stream, payloadBuffer, 0, (int)header.PayloadLength, cancellationToken))
                {
                    throw new EndOfStreamException("Connection terminated prematurely while reading payload.");
                }
            }

            return new SyncPacket(header, payloadBuffer);
        }

        /// <summary>
        /// قراءة عدد محدد من البايتات بدقة والتغلب على تجزئة قراءة TCP
        /// </summary>
        public static async Task<bool> ReadExactBytesAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            int totalBytesRead = 0;
            while (totalBytesRead < count)
            {
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(offset + totalBytesRead, count - totalBytesRead), cancellationToken);
                if (bytesRead == 0)
                {
                    // Stream closed
                    if (totalBytesRead == 0) return false;
                    throw new EndOfStreamException($"Expected {count} bytes, but stream closed after reading {totalBytesRead} bytes.");
                }
                totalBytesRead += bytesRead;
            }
            return true;
        }

        /// <summary>
        /// كتابة حزمة كاملة إلى التدفق بشكل متزامن
        /// </summary>
        public static async Task WritePacketAsync(Stream stream, SyncPacket packet, CancellationToken cancellationToken = default)
        {
            byte[] packetBytes = packet.ToBytes();
            await stream.WriteAsync(packetBytes.AsMemory(), cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
    }
}
