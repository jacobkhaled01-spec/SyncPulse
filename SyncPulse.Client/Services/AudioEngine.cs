using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SyncPulse.Client.Services
{
    /// <summary>
    /// محرك الصوت عالي النقاء والدقة (16 kHz HD Voice PCM Engine) لنظام Windows
    /// </summary>
    public class AudioEngine : IDisposable
    {
        // إعدادات الصوت عالية النقاء: 16000 Hz, 16-bit Mono (32 KB/sec) - معيار HD Voice الدولي
        public const int SampleRate = 16000;
        public const short BitsPerSample = 16;
        public const short Channels = 1;
        public const int BufferDurationMs = 40; // كل 40 مللي ثانية حزمة صوتية
        public const int BufferSize = (SampleRate * Channels * (BitsPerSample / 8) * BufferDurationMs) / 1000; // 1280 بايت

        private IntPtr _hWaveIn = IntPtr.Zero;
        private IntPtr _hWaveOut = IntPtr.Zero;
        private bool _isRecording;
        private bool _isPlaying;
        private WaveInProc? _waveInProc;

        private readonly GCHandle[] _inHeaderHandles = new GCHandle[4];
        private readonly GCHandle[] _inBufferHandles = new GCHandle[4];

        public bool IsMuted { get; set; }

        public event Action<byte[]>? AudioDataCaptured;

        #region WinMM P/Invoke Structs & Constants

        private const int CALLBACK_FUNCTION = 0x00030000;
        private const int WIM_DATA = 0x3C0;
        private const int WOM_DONE = 0x3BD;
        private const int WHDR_PREPARED = 0x00000002;
        private const int WHDR_DONE = 0x00000001;

        private delegate void WaveInProc(IntPtr hwi, int uMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);
        private delegate void WaveOutProc(IntPtr hwo, int uMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEFORMATEX
        {
            public short wFormatTag;
            public short nChannels;
            public int nSamplesPerSec;
            public int nAvgBytesPerSec;
            public short nBlockAlign;
            public short wBitsPerSample;
            public short cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEHDR
        {
            public IntPtr lpData;
            public int dwBufferLength;
            public int dwBytesRecorded;
            public IntPtr dwUser;
            public int dwFlags;
            public int dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveInOpen(out IntPtr phwi, int uDeviceID, ref WAVEFORMATEX lpFormat, WaveInProc dwCallback, IntPtr dwInstance, int fdwOpen);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveInPrepareHeader(IntPtr hwi, IntPtr pwh, int cbwh);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveInAddBuffer(IntPtr hwi, IntPtr pwh, int cbwh);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveInStart(IntPtr hwi);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveInStop(IntPtr hwi);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveInReset(IntPtr hwi);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveInClose(IntPtr hwi);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveOutOpen(out IntPtr phwo, int uDeviceID, ref WAVEFORMATEX lpFormat, IntPtr dwCallback, IntPtr dwInstance, int fdwOpen);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveOutPrepareHeader(IntPtr hwo, IntPtr pwh, int cbwh);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveOutUnprepareHeader(IntPtr hwo, IntPtr pwh, int cbwh);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveOutWrite(IntPtr hwo, IntPtr pwh, int cbwh);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveOutReset(IntPtr hwo);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveOutClose(IntPtr hwo);

        #endregion

        public void Start()
        {
            Stop();

            try
            {
                var format = CreateWaveFormat();

                // 1. تهيئة مكبر الصوت للتشغيل (Playback)
                int outResult = waveOutOpen(out _hWaveOut, -1, ref format, IntPtr.Zero, IntPtr.Zero, 0);
                _isPlaying = (outResult == 0);

                // 2. تهيئة الميكروفون للتسجيل (Capture)
                _waveInProc = OnWaveInData;
                int inResult = waveInOpen(out _hWaveIn, -1, ref format, _waveInProc, IntPtr.Zero, CALLBACK_FUNCTION);

                if (inResult == 0)
                {
                    _isRecording = true;

                    // تهيئة 4 مخازن دائرية للميكروفون بحجم 1280 بايت
                    for (int i = 0; i < 4; i++)
                    {
                        byte[] buffer = new byte[BufferSize];
                        _inBufferHandles[i] = GCHandle.Alloc(buffer, GCHandleType.Pinned);

                        var header = new WAVEHDR
                        {
                            lpData = _inBufferHandles[i].AddrOfPinnedObject(),
                            dwBufferLength = BufferSize,
                            dwBytesRecorded = 0,
                            dwUser = (IntPtr)i,
                            dwFlags = 0
                        };

                        _inHeaderHandles[i] = GCHandle.Alloc(header, GCHandleType.Pinned);
                        waveInPrepareHeader(_hWaveIn, _inHeaderHandles[i].AddrOfPinnedObject(), Marshal.SizeOf<WAVEHDR>());
                        waveInAddBuffer(_hWaveIn, _inHeaderHandles[i].AddrOfPinnedObject(), Marshal.SizeOf<WAVEHDR>());
                    }

                    waveInStart(_hWaveIn);
                }
            }
            catch { }
        }

        private void OnWaveInData(IntPtr hwi, int uMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2)
        {
            if (uMsg != WIM_DATA || !_isRecording || _hWaveIn == IntPtr.Zero) return;

            try
            {
                var header = Marshal.PtrToStructure<WAVEHDR>(dwParam1);
                if (header.dwBytesRecorded > 0)
                {
                    byte[] data = new byte[header.dwBytesRecorded];
                    Marshal.Copy(header.lpData, data, 0, header.dwBytesRecorded);

                    if (!IsMuted)
                    {
                        AudioDataCaptured?.Invoke(data);
                    }
                }

                if (_isRecording && _hWaveIn != IntPtr.Zero)
                {
                    waveInAddBuffer(_hWaveIn, dwParam1, Marshal.SizeOf<WAVEHDR>());
                }
            }
            catch { }
        }

        public void PlayAudioChunk(byte[] pcmData)
        {
            if (!_isPlaying || _hWaveOut == IntPtr.Zero || pcmData == null || pcmData.Length == 0) return;

            try
            {
                var bufferHandle = GCHandle.Alloc(pcmData, GCHandleType.Pinned);
                var header = new WAVEHDR
                {
                    lpData = bufferHandle.AddrOfPinnedObject(),
                    dwBufferLength = pcmData.Length,
                    dwFlags = 0
                };

                IntPtr pHeader = Marshal.AllocHGlobal(Marshal.SizeOf<WAVEHDR>());
                Marshal.StructureToPtr(header, pHeader, false);

                waveOutPrepareHeader(_hWaveOut, pHeader, Marshal.SizeOf<WAVEHDR>());
                waveOutWrite(_hWaveOut, pHeader, Marshal.SizeOf<WAVEHDR>());

                // تحرير المورد لاحقاً بعد انتهاء التشغيل الفعلي
                Task.Delay(120).ContinueWith(_ =>
                {
                    try
                    {
                        if (_hWaveOut != IntPtr.Zero)
                        {
                            waveOutUnprepareHeader(_hWaveOut, pHeader, Marshal.SizeOf<WAVEHDR>());
                        }
                        if (bufferHandle.IsAllocated) bufferHandle.Free();
                        Marshal.FreeHGlobal(pHeader);
                    }
                    catch { }
                });
            }
            catch { }
        }

        private static WAVEFORMATEX CreateWaveFormat()
        {
            var format = new WAVEFORMATEX
            {
                wFormatTag = 1, // PCM
                nChannels = Channels,
                nSamplesPerSec = SampleRate,
                wBitsPerSample = BitsPerSample
            };
            format.nBlockAlign = (short)(format.nChannels * (format.wBitsPerSample / 8));
            format.nAvgBytesPerSec = format.nSamplesPerSec * format.nBlockAlign;
            format.cbSize = 0;
            return format;
        }

        public void Stop()
        {
            _isRecording = false;
            _isPlaying = false;

            try
            {
                if (_hWaveIn != IntPtr.Zero)
                {
                    waveInStop(_hWaveIn);
                    waveInReset(_hWaveIn);
                    waveInClose(_hWaveIn);
                    _hWaveIn = IntPtr.Zero;
                }
            }
            catch { }

            try
            {
                if (_hWaveOut != IntPtr.Zero)
                {
                    waveOutReset(_hWaveOut);
                    waveOutClose(_hWaveOut);
                    _hWaveOut = IntPtr.Zero;
                }
            }
            catch { }

            // تحرير مقابض الذاكرة المثبتة
            for (int i = 0; i < 4; i++)
            {
                try
                {
                    if (_inHeaderHandles[i].IsAllocated) _inHeaderHandles[i].Free();
                    if (_inBufferHandles[i].IsAllocated) _inBufferHandles[i].Free();
                }
                catch { }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
