using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SyncPulse.Client.Services;
using SyncPulse.Client.Utils;
using SyncPulse.Core.Enums;
using SyncPulse.Core.Packets;
using SyncPulse.Core.Protocol;

namespace SyncPulse.Client.ViewModels
{
    public class CallViewModel : INotifyPropertyChanged
    {
        private readonly ClientNetworkService _network;
        private readonly MediaStreamService _media;
        private readonly DispatcherTimer _durationTimer;

        private int _callId;
        private int _targetUserID;
        private string _targetUsername = string.Empty;
        private string _targetDisplayName = string.Empty;
        private CallType _callType = CallType.Audio;
        private CallAction _callState = CallAction.Offer;
        private bool _isIncoming;
        private bool _isMuted;
        private bool _isCameraOff;
        private int _secondsElapsed;

        public int CallID
        {
            get => _callId;
            set { _callId = value; OnPropertyChanged(); }
        }

        public string TargetUsername
        {
            get => _targetUsername;
            set
            {
                _targetUsername = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayUsername));
            }
        }

        public string DisplayUsername => "@" + (_targetUsername ?? string.Empty).Trim().TrimStart('@');

        public string TargetDisplayName
        {
            get => _targetDisplayName;
            set { _targetDisplayName = value; OnPropertyChanged(); OnPropertyChanged(nameof(InitialChar)); }
        }

        public CallType CallType
        {
            get => _callType;
            set { _callType = value; OnPropertyChanged(); OnPropertyChanged(nameof(CallTypeTitle)); }
        }

        public CallAction CallState
        {
            get => _callState;
            set
            {
                _callState = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(IsRinging));
                OnPropertyChanged(nameof(IsConnected));
            }
        }

        public bool IsIncoming
        {
            get => _isIncoming;
            set { _isIncoming = value; OnPropertyChanged(); }
        }

        public bool IsMuted
        {
            get => _isMuted;
            set { _isMuted = value; OnPropertyChanged(); OnPropertyChanged(nameof(MuteButtonText)); }
        }

        public bool IsCameraOff
        {
            get => _isCameraOff;
            set { _isCameraOff = value; OnPropertyChanged(); OnPropertyChanged(nameof(CameraButtonText)); }
        }

        public string DurationText
        {
            get
            {
                var ts = TimeSpan.FromSeconds(_secondsElapsed);
                return ts.ToString(@"mm\:ss");
            }
        }

        public string CallTypeTitle => CallType == CallType.Video ? "مكالمة فيديو مرئية" : "مكالمة صوتية مباشرة";

        public string StateText => CallState switch
        {
            CallAction.Offer => IsIncoming ? "مكالمة واردة..." : "جاري الاتصال...",
            CallAction.Ringing => "يرن الآن...",
            CallAction.Accept => "متصل (جاري البث الحي)",
            CallAction.Reject => "تم رفض المكالمة",
            CallAction.Busy => "الطرف الآخر مشغول في مكالمة أخرى",
            CallAction.Offline => "الطرف الآخر غير متصل بالشبكة حالياً",
            CallAction.End => "تم إنهاء المكالمة",
            _ => "جاري المعالجة..."
        };

        public bool IsRinging => CallState == CallAction.Offer || CallState == CallAction.Ringing;
        public bool IsConnected => CallState == CallAction.Accept;

        public string MuteButtonText => IsMuted ? "🔇 المايك مكتوم" : "🎤 المايك يعمل";
        public string CameraButtonText => IsCameraOff ? "🚫 الكاميرا مغلقة" : "📷 الكاميرا تعمل";

        public string InitialChar => string.IsNullOrEmpty(TargetDisplayName) ? "?" : TargetDisplayName[0].ToString().ToUpper();

        public ICommand AcceptCommand { get; }
        public ICommand RejectCommand { get; }
        public ICommand EndCallCommand { get; }
        public ICommand ToggleMuteCommand { get; }
        public ICommand ToggleCameraCommand { get; }

        public event Action? CallClosed;

        public CallViewModel(ClientNetworkService network, MediaStreamService media)
        {
            _network = network;
            _media = media;

            _durationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _durationTimer.Tick += (s, e) =>
            {
                _secondsElapsed++;
                OnPropertyChanged(nameof(DurationText));
            };

            AcceptCommand = new RelayCommand(async () => await AcceptCallAsync());
            RejectCommand = new RelayCommand(async () => await RejectCallAsync());
            EndCallCommand = new RelayCommand(async () => await EndCallAsync());
            ToggleMuteCommand = new RelayCommand(() => IsMuted = !IsMuted);
            ToggleCameraCommand = new RelayCommand(() => IsCameraOff = !IsCameraOff);
        }

        public void InitializeOutgoing(int targetUserId, string targetUsername, string targetDisplayName, CallType type)
        {
            _targetUserID = targetUserId;
            TargetUsername = targetUsername.Trim().TrimStart('@');
            TargetDisplayName = targetDisplayName;
            CallType = type;
            IsIncoming = false;
            CallState = CallAction.Offer;
            _secondsElapsed = 0;

            _ = SendCallSignalAsync(CallAction.Offer);
        }

        public void InitializeIncoming(CallSignalPacket offer)
        {
            CallID = offer.CallID;
            _targetUserID = offer.CallerID;
            TargetUsername = offer.CallerUsername.Trim().TrimStart('@');
            TargetDisplayName = string.IsNullOrEmpty(offer.CallerDisplayName) ? offer.CallerUsername : offer.CallerDisplayName;
            CallType = offer.Type;
            IsIncoming = true;
            CallState = CallAction.Offer;
            _secondsElapsed = 0;

            _ = SendCallSignalAsync(CallAction.Ringing);
        }

        public void HandleRemoteSignal(CallSignalPacket signal)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                CallState = signal.Action;

                if (signal.Action == CallAction.Accept)
                {
                    CallID = signal.CallID;
                    _durationTimer.Start();
                    _media.Start(_network.Session.ServerIP, _network.Session.UdpPort, CallID, _network.Session.UserID);
                }
                else if (signal.Action == CallAction.Reject || signal.Action == CallAction.End || signal.Action == CallAction.Busy || signal.Action == CallAction.Offline)
                {
                    _durationTimer.Stop();
                    _media.Stop();
                    CloseCallWithDelay();
                }
            });
        }

        private async Task AcceptCallAsync()
        {
            CallState = CallAction.Accept;
            _durationTimer.Start();
            _media.Start(_network.Session.ServerIP, _network.Session.UdpPort, CallID, _network.Session.UserID);
            await SendCallSignalAsync(CallAction.Accept);
        }

        private async Task RejectCallAsync()
        {
            CallState = CallAction.Reject;
            await SendCallSignalAsync(CallAction.Reject);
            CloseCallWithDelay();
        }

        private async Task EndCallAsync()
        {
            _durationTimer.Stop();
            _media.Stop();
            CallState = CallAction.End;
            await SendCallSignalAsync(CallAction.End);
            CloseCallWithDelay();
        }

        private async Task SendCallSignalAsync(CallAction action)
        {
            var packet = new CallSignalPacket
            {
                CallID = CallID,
                CallerID = _network.Session.UserID,
                CallerUsername = _network.Session.Username,
                CallerDisplayName = _network.Session.DisplayName,
                ReceiverID = _targetUserID,
                ReceiverUsername = TargetUsername,
                Action = action,
                Type = CallType,
                Timestamp = DateTime.UtcNow
            };

            var packetType = action switch
            {
                CallAction.Offer => PacketType.CallOffer,
                CallAction.Ringing => PacketType.CallRinging,
                CallAction.Accept => PacketType.CallAnswer,
                CallAction.Reject => PacketType.CallReject,
                CallAction.Busy => PacketType.CallBusy,
                CallAction.Offline => PacketType.CallBusy,
                CallAction.End => PacketType.CallEnd,
                _ => PacketType.CallOffer
            };

            await _network.SendPacketAsync(SyncPacket.Create(packetType, packet));
        }

        private void CloseCallWithDelay()
        {
            Task.Delay(1400).ContinueWith(_ =>
            {
                App.Current?.Dispatcher.Invoke(() => CallClosed?.Invoke());
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
