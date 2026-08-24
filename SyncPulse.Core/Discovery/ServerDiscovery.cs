using System;
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
        private readonly UdpClient _udpClient;
        private CancellationTokenSource? _cts;

        public ServerDiscoveryBroadcaster()
        {
            _udpClient = new UdpClient();
            _udpClient.EnableBroadcast = true;
        }

        public void StartBroadcasting(ServerAnnouncement announcement, int intervalMs = 2000)
        {
            _cts = new CancellationTokenSource();
            Task.Run(async () =>
            {
                var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
                byte[] messageBytes = SerializationUtils.SerializeToUtf8Bytes(announcement);

                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await _udpClient.SendAsync(messageBytes, broadcastEndpoint, _cts.Token);
                        await Task.Delay(intervalMs, _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // Ignore transient network adapter glitches
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
            _udpClient.Dispose();
        }
    }

    /// <summary>
    /// مستشعر العميل لاكتشاف الخادم تلقائياً على شبكة الواي فاي
    /// </summary>
    public class ServerDiscoveryListener : IDisposable
    {
        private readonly UdpClient _udpClient;
        private CancellationTokenSource? _cts;

        public event Action<ServerAnnouncement>? ServerDiscovered;

        public ServerDiscoveryListener()
        {
            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, ServerDiscoveryBroadcaster.DiscoveryPort));
        }

        public void StartListening()
        {
            _cts = new CancellationTokenSource();
            Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _udpClient.ReceiveAsync(_cts.Token);
                        var announcement = SerializationUtils.DeserializeFromUtf8Bytes<ServerAnnouncement>(result.Buffer);

                        if (announcement != null)
                        {
                            // If ServerIP was not explicitly specified, use the sender IP
                            if (string.IsNullOrWhiteSpace(announcement.ServerIP) || announcement.ServerIP == "0.0.0.0")
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
        }

        public void Dispose()
        {
            Stop();
            _udpClient.Dispose();
        }
    }
}
