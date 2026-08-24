using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SyncPulse.Core.Protocol;

namespace SyncPulse.Server.Services
{
    /// <summary>
    /// تمثيل جلسة العميل المتصل عبر مقبس TCP (Thread-Safe Client Session)
    /// </summary>
    public class ClientSession : IDisposable
    {
        private readonly TcpClient _tcpClient;
        private readonly Stream _networkStream;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ClientIP { get; }
        public int ClientPort { get; }
        public DateTime ConnectedAt { get; } = DateTime.UtcNow;
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
        public bool IsAuthenticated => UserID > 0;

        public Stream Stream => _networkStream;

        public ClientSession(TcpClient tcpClient)
        {
            _tcpClient = tcpClient;
            _networkStream = tcpClient.GetStream();

            if (tcpClient.Client.RemoteEndPoint is System.Net.IPEndPoint remoteEp)
            {
                ClientIP = remoteEp.Address.ToString();
                ClientPort = remoteEp.Port;
            }
            else
            {
                ClientIP = "Unknown";
                ClientPort = 0;
            }
        }

        /// <summary>
        /// إرسال حزمة إلى العميل بأمان وخلو تام من تداخل الخيوط (Thread-Safe Send)
        /// </summary>
        public async Task SendPacketAsync(SyncPacket packet, CancellationToken cancellationToken = default)
        {
            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                await FrameStreamParser.WritePacketAsync(_networkStream, packet, cancellationToken);
                LastActiveAt = DateTime.UtcNow;
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public void Close()
        {
            try
            {
                _networkStream.Close();
                _tcpClient.Close();
            }
            catch
            {
                // Ignored during cleanup
            }
        }

        public void Dispose()
        {
            Close();
            _sendLock.Dispose();
            _tcpClient.Dispose();
        }
    }
}
