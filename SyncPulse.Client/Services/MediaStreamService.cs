using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
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

        public AudioEngine Audio { get; } = new();
        public VideoEngine Video { get; } = new();

        public int CurrentCallID { get; private set; }
        public int LocalUserID { get; private set; }
        public CallType ActiveCallType { get; private set; }

        public event Action<MediaFramePacket>? MediaFrameReceived;
        public event Action<BitmapSource>? VideoFrameDecoded;

        public MediaStreamService()
        {
            // ربط التقاط الصوت بالبث عبر UDP
            Audio.AudioDataCaptured += pcmData =>
            {
                if (_isStreaming)
                {
                    SendMediaFrame(CallType.Audio, pcmData);
                }
            };

            // ربط التقاط الفيديو بالبث عبر UDP
            Video.VideoFrameCaptured += jpegData =>
            {
                if (_isStreaming && ActiveCallType == CallType.Video)
                {
                    SendMediaFrame(CallType.Video, jpegData);
                }
            };
        }

        public void Start(string serverIp, int serverUdpPort, int callId, int userId, CallType callType)
        {
            Stop();

            CurrentCallID = callId;
            LocalUserID = userId;
            ActiveCallType = callType;
            _serverMediaEndpoint = new IPEndPoint(IPAddress.Parse(serverIp), serverUdpPort);

            _udpClient = new UdpClient(0);
            _udpClient.Client.SendBufferSize = 2 * 1024 * 1024;
            _udpClient.Client.ReceiveBufferSize = 2 * 1024 * 1024;
            _cts = new CancellationTokenSource();
            _isStreaming = true;

            _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));

            // 1. تشغيل التقاط وتشغيل الصوت الحقيقي
            Audio.Start();

            // 2. تشغيل بث الفيديو المباشر في حال كانت المكالمة مرئية
            if (callType == CallType.Video)
            {
                Video.Start();
            }

            // إرسال حزم تهيئة أولية لتثبيت منفذ UDP في الخادم
            Task.Run(async () =>
            {
                for (int i = 0; i < 3; i++)
                {
                    if (!_isStreaming) break;
                    SendMediaFrame(CallType.Audio, new byte[32]);
                    await Task.Delay(100);
                }
            });
        }

        public async Task SendMediaFrameAsync(CallType mediaType, byte[] payloadData)
        {
            if (!_isStreaming || _udpClient == null || _serverMediaEndpoint == null || payloadData == null || payloadData.Length == 0) return;

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

                            // معالجة الصوت: تشغيل فوري عبر السماعات
                            if (mediaFrame.FrameType == CallType.Audio && mediaFrame.FrameData.Length > 16)
                            {
                                Audio.PlayAudioChunk(mediaFrame.FrameData);
                            }
                            // معالجة الفيديو: فك ضغط الإطار وعرضه فورياً على الشاشة
                            else if (mediaFrame.FrameType == CallType.Video)
                            {
                                if (mediaFrame.FrameData.Length <= 16)
                                {
                                    VideoFrameDecoded?.Invoke(null!);
                                }
                                else
                                {
                                    var bmp = VideoEngine.DecodeFrame(mediaFrame.FrameData);
                                    if (bmp != null)
                                    {
                                        VideoFrameDecoded?.Invoke(bmp);
                                    }
                                }
                            }
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

            Audio.Stop();
            Video.Stop();

            try { _udpClient?.Close(); } catch { }
            _udpClient = null;

            CurrentCallID = 0;
            LocalUserID = 0;
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
            Audio.Dispose();
            Video.Dispose();
        }
    }
}
