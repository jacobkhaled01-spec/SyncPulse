using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SyncPulse.Core.Enums;
using SyncPulse.Core.Packets;
using SyncPulse.Core.Protocol;
using SyncPulse.Core.Utils;

namespace SyncPulse.Server.Engine
{
    /// <summary>
    /// مكرر تدفقات الوسائط المباشرة للصوت والفيديو عالي السرعة عبر UDP Relay (Port 8889)
    /// </summary>
    public class UdpMediaRelay : IDisposable
    {
        public const int DefaultMediaPort = 8889;
        private readonly int _port;
        private UdpClient? _udpServer;
        private readonly ConcurrentDictionary<int, IPEndPoint> _userEndpoints = new();
        private readonly ConcurrentDictionary<int, (int User1, int User2)> _activeCallPairs = new();
        private CancellationTokenSource? _cts;

        public event Action<int, int>? MediaFrameRelayed;

        public UdpMediaRelay(int port = DefaultMediaPort)
        {
            _port = port;
        }

        public void RegisterCallParticipants(int callId, int user1, int user2)
        {
            _activeCallPairs[callId] = (user1, user2);
        }

        public void UnregisterCall(int callId)
        {
            _activeCallPairs.TryRemove(callId, out _);
        }

        public void Start()
        {
            if (_udpServer != null) return;

            try
            {
                _udpServer = new UdpClient();
                _udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpServer.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
                _udpServer.Client.SendBufferSize = 2 * 1024 * 1024;
                _udpServer.Client.ReceiveBufferSize = 2 * 1024 * 1024;
            }
            catch
            {
                _udpServer = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            }

            _cts = new CancellationTokenSource();
            Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested && _udpServer != null)
                {
                    try
                    {
                        var result = await _udpServer.ReceiveAsync(_cts.Token);
                        if (result.Buffer.Length < 12) continue;

                        MediaFramePacket? mediaFrame = null;

                        // 1. فك تأطير ترويسة البروتوكول (12-Byte FrameHeader)
                        if (FrameHeader.TryDeserialize(result.Buffer.AsSpan(0, 12), out FrameHeader header, out _) &&
                            (header.Type == PacketType.AudioFrame || header.Type == PacketType.VideoFrame))
                        {
                            byte[] payload = new byte[header.PayloadLength];
                            Buffer.BlockCopy(result.Buffer, 12, payload, 0, payload.Length);
                            var packet = new SyncPacket(header, payload);
                            mediaFrame = packet.GetPayload<MediaFramePacket>();
                        }
                        else
                        {
                            mediaFrame = SerializationUtils.DeserializeFromUtf8Bytes<MediaFramePacket>(result.Buffer);
                        }

                        if (mediaFrame == null) continue;

                        // تسجيل عنوان المنفذ للطرف المرسل
                        _userEndpoints[mediaFrame.SenderID] = result.RemoteEndPoint;

                        // تمرير الحزمة للطرف الآخر في المكالمة
                        if (_activeCallPairs.TryGetValue(mediaFrame.CallID, out var pair))
                        {
                            int targetUserId = (mediaFrame.SenderID == pair.User1) ? pair.User2 : pair.User1;

                            if (_userEndpoints.TryGetValue(targetUserId, out var targetEndPoint))
                            {
                                await _udpServer.SendAsync(result.Buffer, result.Buffer.Length, targetEndPoint);
                                MediaFrameRelayed?.Invoke(mediaFrame.CallID, result.Buffer.Length);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // Ignore transient UDP packet drops
                    }
                }
            }, _cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _udpServer?.Dispose();
            _udpServer = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
