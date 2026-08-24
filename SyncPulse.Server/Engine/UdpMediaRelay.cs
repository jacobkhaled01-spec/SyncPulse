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
                _udpServer.Client.SendBufferSize = 64 * 1024;
                _udpServer.Client.ReceiveBufferSize = 64 * 1024;
            }
            catch
            {
                // Fallback to dynamic port if busy
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
                        if (result.Buffer.Length == 0) continue;

                        var mediaFrame = SerializationUtils.DeserializeFromUtf8Bytes<MediaFramePacket>(result.Buffer);
                        if (mediaFrame == null) continue;

                        _userEndpoints[mediaFrame.SenderID] = result.RemoteEndPoint;

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
                        // Ignore transient UDP errors
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
