using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SyncPulse.Client.Services
{
    /// <summary>
    /// محرك الفيديو المباشر عالي السرعة لالتقاط وبث الإطارات المرئية (Live Video Streaming Engine)
    /// </summary>
    public class VideoEngine : IDisposable
    {
        private CancellationTokenSource? _cts;
        private bool _isCapturing;

        public bool IsCameraOff { get; set; }
        public int FrameWidth { get; set; } = 320;
        public int FrameHeight { get; set; } = 240;

        public event Action<byte[]>? VideoFrameCaptured;

        #region GDI Screen/Webcam Frame Capture P/Invoke

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

            _cts = new CancellationTokenSource();
            _isCapturing = true;

            Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested && _isCapturing)
                {
                    try
                    {
                        if (!IsCameraOff)
                        {
                            byte[]? frameData = CaptureCurrentFrame();
                            if (frameData != null && frameData.Length > 0)
                            {
                                VideoFrameCaptured?.Invoke(frameData);
                            }
                        }

                        // 15 إطار في الثانية (كل 66 مللي ثانية) للبث السلس عبر الشبكة
                        await Task.Delay(66, _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch { }
                }
            }, _cts.Token);
        }

        private byte[]? CaptureCurrentFrame()
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

                var bmpSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBmp,
                    IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                bmpSource.Freeze();

                var encoder = new JpegBitmapEncoder { QualityLevel = 50 };
                encoder.Frames.Add(BitmapFrame.Create(bmpSource));

                using var ms = new MemoryStream();
                encoder.Save(ms);
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

        public void Stop()
        {
            _isCapturing = false;
            _cts?.Cancel();
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
