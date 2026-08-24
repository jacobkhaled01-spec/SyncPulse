using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SyncPulse.Client.Models
{
    public class ContactItem : INotifyPropertyChanged
    {
        private string _displayName = string.Empty;
        private string _lastMessage = "لا توجد رسائل سابقة";
        private DateTime _lastMessageTime = DateTime.MinValue;
        private int _unreadCount;
        private bool _isOnline;
        private bool _isSelected;
        private bool _isTyping;

        public int ContactUserID { get; set; }
        public string Username { get; set; } = string.Empty;

        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }

        public string LastMessage
        {
            get => _lastMessage;
            set { _lastMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplaySnippet)); }
        }

        public DateTime LastMessageTime
        {
            get => _lastMessageTime;
            set { _lastMessageTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedTime)); }
        }

        public string FormattedTime => LastMessageTime == DateTime.MinValue ? string.Empty : LastMessageTime.ToLocalTime().ToString("HH:mm");

        public int UnreadCount
        {
            get => _unreadCount;
            set { _unreadCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnread)); }
        }

        public bool HasUnread => _unreadCount > 0;

        public bool IsOnline
        {
            get => _isOnline;
            set { _isOnline = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); OnPropertyChanged(nameof(StatusText)); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public bool IsTyping
        {
            get => _isTyping;
            set
            {
                _isTyping = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(DisplaySnippet));
            }
        }

        public string StatusColor => IsTyping ? "#2563EB" : (IsOnline ? "#10B981" : "#94A3B8");
        public string StatusText => IsTyping ? "✍️ يكتب الآن..." : (IsOnline ? "متصل الآن" : "غير متصل");
        public string DisplaySnippet => IsTyping ? "✍️ يكتب الآن..." : LastMessage;

        // لون الأفاتار المولد ديناميكياً
        public string AvatarBackground
        {
            get
            {
                string[] colors = { "#2563EB", "#0284C7", "#7C3AED", "#059669", "#D97706", "#DB2777" };
                int hash = Math.Abs(Username.GetHashCode());
                return colors[hash % colors.Length];
            }
        }

        public string InitialChar => string.IsNullOrEmpty(DisplayName) ? (string.IsNullOrEmpty(Username) ? "?" : Username[0].ToString().ToUpper()) : DisplayName[0].ToString().ToUpper();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
