using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SyncPulse.Core.Utils;

namespace SyncPulse.Core.Discovery
{
    public class ServerAnnouncement
    {
        public string ServerName { get; set; } = "SecureTalk Local Server";
        public string ServerIP { get; set; } = string.Empty;
        public int TcpPort { get; set; } = 8888;
        public int UdpPort { get; set; } = 8889;
        public string Version { get; set; } = "1.0.0";
    }

    /// <summary>
    /// خدمة الاكتشاف التلقائي للخادم عبر شبكات الواي فاي المحلية (UDP Auto-Discovery Service - Port 8887)
    /// </summary>
    public class ServerDiscoveryBroadcaster : IDisposable
    {
        public const int DiscoveryPort = 8887;
        private UdpClient? _udpClient;
        private CancellationTokenSource? _cts;

        public void StartBroadcasting(ServerAnnouncement announcement, int intervalMs = 1500)
        {
            Stop();

            try
            {
                _udpClient = new UdpClient();
                _udpClient.EnableBroadcast = true;
            }
            catch
            {
                return;
            }

            _cts = new CancellationTokenSource();
            Task.Run(async () =>
            {
                byte[] messageBytes = SerializationUtils.SerializeToUtf8Bytes(announcement);

                while (!_cts.Token.IsCancellationRequested && _udpClient != null)
                {
                    try
                    {
                        var targets = NetworkUtils.GetBroadcastAddresses();
                        foreach (var target in targets)
                        {
                            var broadcastEndpoint = new IPEndPoint(target, DiscoveryPort);
                            await _udpClient.SendAsync(messageBytes, broadcastEndpoint, _cts.Token);
                        }

                        await Task.Delay(intervalMs, _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // Ignore transient glitches
                    }
                }
            }, _cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _udpClient?.Dispose();
            _udpClient = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }

    /// <summary>
    /// مستشعر العميل لاكتشاف الخادم تلقائياً على شبكة الواي فاي
    /// </summary>
    public class ServerDiscoveryListener : IDisposable
    {
        private UdpClient? _udpClient;
        private CancellationTokenSource? _cts;

        public event Action<ServerAnnouncement>? ServerDiscovered;

        public void StartListening()
        {
            Stop();

            try
            {
                _udpClient = new UdpClient();
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, ServerDiscoveryBroadcaster.DiscoveryPort));
            }
            catch
            {
                return;
            }

            _cts = new CancellationTokenSource();
            Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested && _udpClient != null)
                {
                    try
                    {
                        var result = await _udpClient.ReceiveAsync(_cts.Token);
                        var announcement = SerializationUtils.DeserializeFromUtf8Bytes<ServerAnnouncement>(result.Buffer);

                        if (announcement != null)
                        {
                            if (string.IsNullOrWhiteSpace(announcement.ServerIP) || announcement.ServerIP == "0.0.0.0" || announcement.ServerIP == "127.0.0.1")
                            {
                                announcement.ServerIP = result.RemoteEndPoint.Address.ToString();
                            }

                            ServerDiscovered?.Invoke(announcement);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // Ignore transient network errors
                    }
                }
            }, _cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _udpClient?.Dispose();
            _udpClient = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
