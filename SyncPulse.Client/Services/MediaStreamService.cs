using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SyncPulse.Core.Enums;
using SyncPulse.Core.Packets;
using SyncPulse.Core.Protocol;

namespace SyncPulse.Client.Services
{
    public class MediaStreamService : IDisposable
    {
        private UdpClient? _udpClient;
        private IPEndPoint? _serverMediaEndpoint;
        private CancellationTokenSource? _cts;
        private Task? _receiveTask;
        private bool _isStreaming;
        private uint _frameSeq = 1;

        public int CurrentCallID { get; private set; }
        public int LocalUserID { get; private set; }

        public event Action<MediaFramePacket>? MediaFrameReceived;

        public void Start(string serverIp, int serverUdpPort, int callId, int userId)
        {
            Stop();

            CurrentCallID = callId;
            LocalUserID = userId;
            _serverMediaEndpoint = new IPEndPoint(IPAddress.Parse(serverIp), serverUdpPort);

            _udpClient = new UdpClient(0);
            _cts = new CancellationTokenSource();
            _isStreaming = true;

            _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));

            // Send initial ping frame to register with UDP Relay
            SendMediaFrame(CallType.Audio, new byte[16]);
        }

        public async Task SendMediaFrameAsync(CallType mediaType, byte[] payloadData)
        {
            if (!_isStreaming || _udpClient == null || _serverMediaEndpoint == null) return;

            var frame = new MediaFramePacket
            {
                CallID = CurrentCallID,
                SenderID = LocalUserID,
                FrameType = mediaType,
                SequenceNumber = Interlocked.Increment(ref _frameSeq),
                FrameData = payloadData,
                Timestamp = DateTime.UtcNow
            };

            var packetType = mediaType == CallType.Video ? PacketType.VideoFrame : PacketType.AudioFrame;
            var packet = SyncPacket.Create(packetType, frame);
            byte[] bytes = packet.ToBytes();

            try
            {
                await _udpClient.SendAsync(bytes, bytes.Length, _serverMediaEndpoint);
            }
            catch { }
        }

        public void SendMediaFrame(CallType mediaType, byte[] payloadData)
        {
            _ = SendMediaFrameAsync(mediaType, payloadData);
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _udpClient != null)
                {
                    var result = await _udpClient.ReceiveAsync(ct);
                    if (result.Buffer.Length < 12) continue;

                    if (FrameHeader.TryDeserialize(result.Buffer.AsSpan(0, 12), out FrameHeader header, out _) &&
                        (header.Type == PacketType.AudioFrame || header.Type == PacketType.VideoFrame))
                    {
                        byte[] payload = new byte[header.PayloadLength];
                        Buffer.BlockCopy(result.Buffer, 12, payload, 0, payload.Length);

                        var packet = new SyncPacket(header, payload);
                        var mediaFrame = packet.GetPayload<MediaFramePacket>();
                        if (mediaFrame != null && mediaFrame.SenderID != LocalUserID)
                        {
                            MediaFrameReceived?.Invoke(mediaFrame);
                        }
                    }
                }
            }
            catch { }
        }

        public void Stop()
        {
            _isStreaming = false;
            _cts?.Cancel();

            try { _udpClient?.Close(); } catch { }
            _udpClient = null;

            CurrentCallID = 0;
            LocalUserID = 0;
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
