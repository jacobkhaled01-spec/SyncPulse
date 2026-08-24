using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SyncPulse.Client.Services;
using SyncPulse.Client.Views;
using SyncPulse.Core.Enums;
using SyncPulse.Core.Packets;

namespace SyncPulse.Client.ViewModels
{
    public class ClientMainViewModel : INotifyPropertyChanged
    {
        private readonly ClientNetworkService _network;
        private readonly MediaStreamService _media;

        private object _currentView;
        private AuthViewModel _authVM;
        private ChatViewModel? _chatVM;
        private CallViewModel? _activeCallVM;
        private CallWindow? _activeCallWindow;

        public object CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        public ClientMainViewModel()
        {
            _network = new ClientNetworkService();
            _media = new MediaStreamService();

            _authVM = new AuthViewModel(_network);
            _authVM.AuthenticatedSuccessfully += OnAuthenticated;

            _currentView = _authVM;

            _network.CallSignalReceived += OnCallSignalReceived;
        }

        private void OnAuthenticated()
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                _chatVM = new ChatViewModel(_network);
                _chatVM.CallInitiated += OnOutgoingCallInitiated;
                _chatVM.LoggedOut += () =>
                {
                    _authVM = new AuthViewModel(_network);
                    _authVM.AuthenticatedSuccessfully += OnAuthenticated;
                    CurrentView = _authVM;
                };

                CurrentView = _chatVM;
            });
        }

        private void OnOutgoingCallInitiated(int targetUserId, string targetUsername, string targetDisplayName, CallType callType)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                _activeCallVM = new CallViewModel(_network, _media);
                _activeCallVM.InitializeOutgoing(targetUserId, targetUsername, targetDisplayName, callType);

                _activeCallWindow = new CallWindow
                {
                    DataContext = _activeCallVM,
                    Owner = App.Current?.MainWindow
                };

                _activeCallVM.CallClosed += () =>
                {
                    _activeCallWindow?.Close();
                    _activeCallVM = null;
                    _activeCallWindow = null;
                };
                _activeCallWindow.Show();
            });
        }

        private void OnCallSignalReceived(CallSignalPacket signal)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                if (signal.Action == CallAction.Offer)
                {
                    _activeCallVM = new CallViewModel(_network, _media);
                    _activeCallVM.InitializeIncoming(signal);

                    _activeCallWindow = new CallWindow
                    {
                        DataContext = _activeCallVM,
                        Owner = App.Current?.MainWindow
                    };

                    _activeCallVM.CallClosed += () =>
                    {
                        _activeCallWindow?.Close();
                        _activeCallVM = null;
                        _activeCallWindow = null;
                    };
                    _activeCallWindow.Show();
                }
                else
                {
                    // تمرير إشارات القبول والرنين والرفض والإنهاء لنموذج المكالمة النشط
                    _activeCallVM?.HandleRemoteSignal(signal);
                }
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
