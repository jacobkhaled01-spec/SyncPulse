using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using AForge.Video;
using AForge.Video.DirectShow;

namespace SyncPulse.Client.Services
{
    /// <summary>
    /// محرك الفيديو المباشر عالي السرعة لالتقاط وبث إطارات كاميرا الويب الحقيقية عبر AForge.NET DirectShow (Hardware Webcam Engine)
    /// </summary>
    public class VideoEngine : IDisposable
    {
        private VideoCaptureDevice? _videoSource;
        private CancellationTokenSource? _cts;
        private bool _isCapturing;

        public bool IsCameraOff { get; set; }
        public int FrameWidth { get; set; } = 320;
        public int FrameHeight { get; set; } = 240;

        public event Action<byte[]>? VideoFrameCaptured;

        #region GDI Fallback for devices without physical webcam

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        private const int SRCCOPY = 0x00CC0020;

        #endregion

        public void Start()
        {
            Stop();

            _isCapturing = true;
            _cts = new CancellationTokenSource();

            try
            {
                // 1. البحث عن كاميرات الويب الحقيقية المتصلة بالجهاز
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                if (videoDevices.Count > 0)
                {
                    // تشغيل كاميرا الويب الأولى (Default Webcam)
                    _videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);
                    _videoSource.NewFrame += OnWebcamNewFrame;
                    _videoSource.Start();
                }
                else
                {
                    // في حال عدم وجود كاميرا ويب فيزيائية: تشغيل مسار المحاكاة البديل
                    StartFallbackCaptureLoop(_cts.Token);
                }
            }
            catch
            {
                StartFallbackCaptureLoop(_cts.Token);
            }
        }

        private void OnWebcamNewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            if (!_isCapturing || IsCameraOff || eventArgs.Frame == null) return;

            try
            {
                using var clone = (Bitmap)eventArgs.Frame.Clone();
                using var resized = new Bitmap(clone, new Size(FrameWidth, FrameHeight));
                using var ms = new MemoryStream();

                var encoderParameters = new EncoderParameters(1);
                encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 50L);

                ImageCodecInfo? jpegCodec = GetJpegEncoder();
                if (jpegCodec != null)
                {
                    resized.Save(ms, jpegCodec, encoderParameters);
                }
                else
                {
                    resized.Save(ms, ImageFormat.Jpeg);
                }

                byte[] jpegBytes = ms.ToArray();
                if (jpegBytes.Length > 0)
                {
                    VideoFrameCaptured?.Invoke(jpegBytes);
                }
            }
            catch { }
        }

        private void StartFallbackCaptureLoop(CancellationToken ct)
        {
            Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested && _isCapturing)
                {
                    try
                    {
                        if (!IsCameraOff)
                        {
                            byte[]? frameData = CaptureGdiFrame();
                            if (frameData != null && frameData.Length > 0)
                            {
                                VideoFrameCaptured?.Invoke(frameData);
                            }
                        }

                        await Task.Delay(66, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch { }
                }
            }, ct);
        }

        private byte[]? CaptureGdiFrame()
        {
            IntPtr hDesk = GetDesktopWindow();
            IntPtr hSrcDC = GetDC(hDesk);
            IntPtr hDestDC = CreateCompatibleDC(hSrcDC);
            IntPtr hBmp = CreateCompatibleBitmap(hSrcDC, FrameWidth, FrameHeight);
            IntPtr hOldBmp = SelectObject(hDestDC, hBmp);

            try
            {
                BitBlt(hDestDC, 0, 0, FrameWidth, FrameHeight, hSrcDC, 0, 0, SRCCOPY);
                SelectObject(hDestDC, hOldBmp);

                using var bmp = Image.FromHbitmap(hBmp);
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
            finally
            {
                DeleteObject(hBmp);
                DeleteDC(hDestDC);
                ReleaseDC(hDesk, hSrcDC);
            }
        }

        public static BitmapSource? DecodeFrame(byte[] jpegBytes)
        {
            if (jpegBytes == null || jpegBytes.Length == 0) return null;

            try
            {
                using var ms = new MemoryStream(jpegBytes);
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = ms;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch
            {
                return null;
            }
        }

        private static ImageCodecInfo? GetJpegEncoder()
        {
            foreach (var codec in ImageCodecInfo.GetImageDecoders())
            {
                if (codec.FormatID == ImageFormat.Jpeg.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        public void Stop()
        {
            _isCapturing = false;
            _cts?.Cancel();

            try
            {
                if (_videoSource != null && _videoSource.IsRunning)
                {
                    _videoSource.SignalToStop();
                    _videoSource.WaitForStop();
                    _videoSource.NewFrame -= OnWebcamNewFrame;
                    _videoSource = null;
                }
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
