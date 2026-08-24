using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
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

        public int MessageID { get; set; }
        public int ConversationID { get; set; }
        public int SenderID { get; set; }
        public string SenderUsername { get; set; } = string.Empty;
        public int ReceiverID { get; set; }
        public string Content { get; set; } = string.Empty;

        public string? AttachmentPath
        {
            get => _attachmentPath;
            set
            {
                _attachmentPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAttachment));
                OnPropertyChanged(nameof(DisplayFileName));
                OnPropertyChanged(nameof(IsImageAttachment));
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
            }
        }

        public byte[]? AttachmentData
        {
            get => _attachmentData;
            set
            {
                _attachmentData = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAttachment));
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
                string fn = DisplayFileName.ToLowerInvariant();
                return fn.EndsWith(".png") || fn.EndsWith(".jpg") || fn.EndsWith(".jpeg") || fn.EndsWith(".bmp") || fn.EndsWith(".gif");
            }
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

        // فقاعة الرسالة (Sender: Soft Blue, Receiver: Pure White)
        public string BubbleBackground => IsOutgoing ? "#EFF6FF" : "#FFFFFF";
        public string BubbleBorder => IsOutgoing ? "#BFDBFE" : "#E2E8F0";
        public string TextColor => "#0F172A";
        public string Alignment => IsOutgoing ? "Left" : "Right";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
