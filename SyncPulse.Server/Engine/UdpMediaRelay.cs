using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SyncPulse.Core.Packets;
using SyncPulse.Core.Utils;

namespace SyncPulse.Server.Engine
{
    /// <summary>
    /// مكرر تدفقات الوسائط المباشرة للصوت والفيديو عبر UDP Relay (Port 8889)
    /// </summary>
    public class UdpMediaRelay : IDisposable
    {
        public const int DefaultMediaPort = 8889;
        private readonly UdpClient _udpServer;
        private readonly ConcurrentDictionary<int, IPEndPoint> _userEndpoints = new(); // UserID -> IPEndPoint
        private readonly ConcurrentDictionary<int, (int User1, int User2)> _activeCallPairs = new(); // CallID -> (User1, User2)
        private CancellationTokenSource? _cts;

        public event Action<int, int>? MediaFrameRelayed; // (CallID, BytesCount)

        public UdpMediaRelay(int port = DefaultMediaPort)
        {
            _udpServer = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            _udpServer.Client.SendBufferSize = 64 * 1024;
            _udpServer.Client.ReceiveBufferSize = 64 * 1024;
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
            _cts = new CancellationTokenSource();
            Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _udpServer.ReceiveAsync(_cts.Token);
                        if (result.Buffer.Length == 0) continue;

                        // فك حزمة الوسائط
                        var mediaFrame = SerializationUtils.DeserializeFromUtf8Bytes<MediaFramePacket>(result.Buffer);
                        if (mediaFrame == null) continue;

                        // تحديث نقطة اتصال المرسل الحالية
                        _userEndpoints[mediaFrame.SenderID] = result.RemoteEndPoint;

                        // البحث عن الطرف المقابل في المكالمة
                        if (_activeCallPairs.TryGetValue(mediaFrame.CallID, out var pair))
                        {
                            int targetUserId = (mediaFrame.SenderID == pair.User1) ? pair.User2 : pair.User1;

                            if (_userEndpoints.TryGetValue(targetUserId, out var targetEndPoint))
                            {
                                await _udpServer.SendAsync(result.Buffer, targetEndPoint, _cts.Token);
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
                        // Ignore transient UDP network socket errors to keep server running
                    }
                }
            }, _cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        public void Dispose()
        {
            Stop();
            _udpServer.Dispose();
        }
    }
}
