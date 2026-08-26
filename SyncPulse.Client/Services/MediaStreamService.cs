using System;
using System.Collections.Concurrent;
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
    /// <summary>
    /// خدمة بث الوسائط المزدوجة فائقة السرعة والموثوقية (Dual-Channel UDP + TCP Hybrid Media Stream Service)
    /// </summary>
    public class MediaStreamService : IDisposable
    {
        private readonly ClientNetworkService _network;
        private UdpClient? _udpClient;
        private IPEndPoint? _serverMediaEndpoint;
        private CancellationTokenSource? _cts;
        private Task? _receiveTask;
        private bool _isStreaming;
        private uint _frameSeq = 1;
        private readonly ConcurrentDictionary<long, byte> _processedFrames = new();

        public AudioEngine Audio { get; } = new();
        public VideoEngine Video { get; } = new();

        public int CurrentCallID { get; private set; }
        public int LocalUserID { get; private set; }
        public CallType ActiveCallType { get; private set; }

        public event Action<MediaFramePacket>? MediaFrameReceived;
        public event Action<BitmapSource?>? VideoFrameDecoded;

        public MediaStreamService(ClientNetworkService network)
        {
            _network = network;

            // ربط استقبال الوسائط عبر قناة TCP الموثوقة كمسار مزدوج يضمن وصول الصوت والصورة 100%
            _network.MediaFrameReceived += frame =>
            {
                if (_isStreaming && frame.CallID == CurrentCallID && frame.SenderID != LocalUserID)
                {
                    ProcessIncomingMediaFrame(frame);
                }
            };

            // ربط التقاط الصوت بالبث عبر القنوات المباشرة
            Audio.AudioDataCaptured += pcmData =>
            {
                if (_isStreaming)
                {
                    SendMediaFrame(CallType.Audio, pcmData);
                }
            };

            // ربط التقاط الفيديو بالبث عبر القنوات المباشرة
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
            _processedFrames.Clear();

            try
            {
                _serverMediaEndpoint = new IPEndPoint(IPAddress.Parse(serverIp), serverUdpPort);
                _udpClient = new UdpClient(0);
                _udpClient.Client.SendBufferSize = 2 * 1024 * 1024;
                _udpClient.Client.ReceiveBufferSize = 2 * 1024 * 1024;
            }
            catch
            {
                _udpClient = null;
            }

            _cts = new CancellationTokenSource();
            _isStreaming = true;

            if (_udpClient != null)
            {
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
            }

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
                for (int i = 0; i < 4; i++)
                {
                    if (!_isStreaming) break;
                    SendMediaFrame(CallType.Audio, new byte[32]);
                    await Task.Delay(80);
                }
            });
        }

        public async Task SendMediaFrameAsync(CallType mediaType, byte[] payloadData)
        {
            if (!_isStreaming || payloadData == null || payloadData.Length == 0) return;

            uint seq = Interlocked.Increment(ref _frameSeq);
            var frame = new MediaFramePacket
            {
                CallID = CurrentCallID,
                SenderID = LocalUserID,
                FrameType = mediaType,
                SequenceNumber = seq,
                FrameData = payloadData,
                Timestamp = DateTime.UtcNow
            };

            var packetType = mediaType == CallType.Video ? PacketType.VideoFrame : PacketType.AudioFrame;
            var packet = SyncPacket.Create(packetType, frame, seq);

            // 1. الإرسال عالي السرعة عبر UDP
            if (_udpClient != null && _serverMediaEndpoint != null)
            {
                try
                {
                    byte[] bytes = packet.ToBytes();
                    await _udpClient.SendAsync(bytes, bytes.Length, _serverMediaEndpoint);
                }
                catch { }
            }

            // 2. الإرسال الموازي المضمون عبر قناة TCP (لضمان تجاوز جدران الحماية ورواترات الواي فاي 100%)
            try
            {
                _ = _network.SendPacketAsync(packet);
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
                            ProcessIncomingMediaFrame(mediaFrame);
                        }
                    }
                }
            }
            catch { }
        }

        private void ProcessIncomingMediaFrame(MediaFramePacket mediaFrame)
        {
            // منع المعالجة المكررة لنفس الإطار عند وصوله من UDP و TCP معاً
            long frameKey = ((long)mediaFrame.FrameType << 32) | (long)mediaFrame.SequenceNumber;
            if (mediaFrame.SequenceNumber > 0 && !_processedFrames.TryAdd(frameKey, 0))
            {
                return;
            }

            // تنظيف دوري لسجل الأرقام المتسلسلة القديمة
            if (_processedFrames.Count > 1000)
            {
                _processedFrames.Clear();
            }

            MediaFrameReceived?.Invoke(mediaFrame);

            // أ. معالجة الصوت: تشغيل فوري عبر السماعات
            if (mediaFrame.FrameType == CallType.Audio && mediaFrame.FrameData.Length > 16)
            {
                Audio.PlayAudioChunk(mediaFrame.FrameData);
            }
            // ب. معالجة الفيديو: فك ضغط الإطار وعرضه فورياً على الشاشة
            else if (mediaFrame.FrameType == CallType.Video)
            {
                if (mediaFrame.FrameData.Length <= 16)
                {
                    VideoFrameDecoded?.Invoke(null);
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
            _processedFrames.Clear();
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
