using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using SyncPulse.Core.Enums;

namespace SyncPulse.Client.Models
{
    public class MessageItem : INotifyPropertyChanged
    {
        private MessageStatus _status;
        private string? _attachmentPath;
        private string? _attachmentFileName;
        private byte[]? _attachmentData;
        private long _attachmentSize;
        private BitmapSource? _imagePreviewSource;
        private bool _imageLoaded;

        public int MessageID { get; set; }
        public int ConversationID { get; set; }
        public int SenderID { get; set; }
        public string SenderUsername { get; set; } = string.Empty;
        public int ReceiverID { get; set; }
        public string Content { get; set; } = string.Empty;

        public bool HasTextContent => !string.IsNullOrWhiteSpace(Content) && Content != DisplayFileName;

        public string? AttachmentPath
        {
            get => _attachmentPath;
            set
            {
                _attachmentPath = value;
                _imageLoaded = false;
                _imagePreviewSource = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAttachment));
                OnPropertyChanged(nameof(DisplayFileName));
                OnPropertyChanged(nameof(IsImageAttachment));
                OnPropertyChanged(nameof(IsAudioAttachment));
                OnPropertyChanged(nameof(IsGeneralFile));
                OnPropertyChanged(nameof(FileIcon));
                OnPropertyChanged(nameof(ImagePreviewSource));
            }
        }

        public string? AttachmentFileName
        {
            get => _attachmentFileName;
            set
            {
                _attachmentFileName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayFileName));
                OnPropertyChanged(nameof(IsImageAttachment));
                OnPropertyChanged(nameof(IsAudioAttachment));
                OnPropertyChanged(nameof(IsGeneralFile));
                OnPropertyChanged(nameof(FileIcon));
            }
        }

        public byte[]? AttachmentData
        {
            get => _attachmentData;
            set
            {
                _attachmentData = value;
                _imageLoaded = false;
                _imagePreviewSource = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAttachment));
                OnPropertyChanged(nameof(ImagePreviewSource));
            }
        }

        public long AttachmentSize
        {
            get => _attachmentSize;
            set
            {
                _attachmentSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormattedSize));
            }
        }

        public bool HasAttachment => !string.IsNullOrEmpty(AttachmentPath) || (AttachmentData != null && AttachmentData.Length > 0);

        public string DisplayFileName
        {
            get
            {
                if (!string.IsNullOrEmpty(AttachmentFileName)) return AttachmentFileName;
                if (!string.IsNullOrEmpty(AttachmentPath)) return Path.GetFileName(AttachmentPath);
                return "ملف مرفق";
            }
        }

        public string FormattedSize
        {
            get
            {
                long bytes = AttachmentSize;
                if (bytes <= 0 && AttachmentData != null) bytes = AttachmentData.Length;
                if (bytes <= 0 && !string.IsNullOrEmpty(AttachmentPath) && File.Exists(AttachmentPath))
                {
                    try { bytes = new FileInfo(AttachmentPath).Length; } catch { }
                }

                if (bytes < 1024) return $"{bytes} B";
                if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
                return $"{(bytes / (1024.0 * 1024.0)):F2} MB";
            }
        }

        public bool IsImageAttachment
        {
            get
            {
                if (!HasAttachment) return false;
                string fn = DisplayFileName.ToLowerInvariant();
                return fn.EndsWith(".png") || fn.EndsWith(".jpg") || fn.EndsWith(".jpeg") || fn.EndsWith(".bmp") || fn.EndsWith(".gif") || fn.EndsWith(".webp");
            }
        }

        public bool IsAudioAttachment
        {
            get
            {
                if (!HasAttachment) return false;
                string fn = DisplayFileName.ToLowerInvariant();
                return fn.EndsWith(".mp3") || fn.EndsWith(".wav") || fn.EndsWith(".m4a") || fn.EndsWith(".aac") || fn.EndsWith(".ogg") || fn.EndsWith(".wma");
            }
        }

        public bool IsGeneralFile => HasAttachment && !IsImageAttachment && !IsAudioAttachment;

        public string FileIcon => DisplayFileName.ToLowerInvariant() switch
        {
            var s when s.EndsWith(".pdf") => "📕",
            var s when s.EndsWith(".zip") || s.EndsWith(".rar") || s.EndsWith(".7z") => "📦",
            var s when s.EndsWith(".doc") || s.EndsWith(".docx") || s.EndsWith(".txt") => "📄",
            var s when s.EndsWith(".xls") || s.EndsWith(".xlsx") => "📊",
            var s when s.EndsWith(".mp4") || s.EndsWith(".mkv") || s.EndsWith(".avi") => "🎬",
            _ => "📁"
        };

        public BitmapSource? ImagePreviewSource
        {
            get
            {
                if (!_imageLoaded)
                {
                    _imageLoaded = true;
                    _imagePreviewSource = LoadImagePreview();
                }
                return _imagePreviewSource;
            }
        }

        private BitmapSource? LoadImagePreview()
        {
            if (!IsImageAttachment) return null;

            try
            {
                if (AttachmentData != null && AttachmentData.Length > 0)
                {
                    using var ms = new MemoryStream(AttachmentData);
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CreateOptions = BitmapCreateOptions.None;
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = ms;
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
                else if (!string.IsNullOrEmpty(AttachmentPath) && File.Exists(AttachmentPath))
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CreateOptions = BitmapCreateOptions.None;
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(AttachmentPath, UriKind.Absolute);
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
            }
            catch { }
            return null;
        }

        public DateTime Timestamp { get; set; }
        public bool IsOutgoing { get; set; }

        public MessageStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusIcon));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        public string FormattedTime => Timestamp.ToLocalTime().ToString("HH:mm");

        public string StatusIcon => Status switch
        {
            MessageStatus.Sent => "✓",
            MessageStatus.Delivered => "✓✓",
            MessageStatus.Read => "✓✓",
            _ => "•"
        };

        public string StatusColor => Status switch
        {
            MessageStatus.Read => "#0284C7", // أزرق ملكي عند القراءة
            MessageStatus.Delivered => "#64748B", // رمادي عند الاستلام
            _ => "#94A3B8" // رمادي فاتح عند الإرسال
        };

        // فقاعة الرسالة بنمط تليجرام الحديث
        public string BubbleBackground => IsOutgoing ? "#EFF6FF" : "#FFFFFF";
        public string BubbleBorder => IsOutgoing ? "#BFDBFE" : "#E2E8F0";
        public string TextColor => "#0F172A";
        public string Alignment => IsOutgoing ? "Left" : "Right";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
