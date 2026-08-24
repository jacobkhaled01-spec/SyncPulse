using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SyncPulse.Core.Enums;

namespace SyncPulse.Client.Models
{
    public class MessageItem : INotifyPropertyChanged
    {
        private MessageStatus _status;

        public int MessageID { get; set; }
        public int ConversationID { get; set; }
        public int SenderID { get; set; }
        public string SenderUsername { get; set; } = string.Empty;
        public int ReceiverID { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? AttachmentPath { get; set; }
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
            MessageStatus.Read => "#0284C7", // Cyan / Blue double check
            MessageStatus.Delivered => "#64748B", // Gray double check
            _ => "#94A3B8" // Single check
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
