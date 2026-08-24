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
            _chatVM = new ChatViewModel(_network);
            _chatVM.CallInitiated += OnOutgoingCallInitiated;
            _chatVM.LoggedOut += () =>
            {
                _authVM = new AuthViewModel(_network);
                _authVM.AuthenticatedSuccessfully += OnAuthenticated;
                CurrentView = _authVM;
            };

            CurrentView = _chatVM;
        }

        private void OnOutgoingCallInitiated(int targetUserId, string targetUsername, string targetDisplayName, CallType callType)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                var callVM = new CallViewModel(_network, _media);
                callVM.InitializeOutgoing(targetUserId, targetUsername, targetDisplayName, callType);

                var callWindow = new CallWindow
                {
                    DataContext = callVM,
                    Owner = App.Current?.MainWindow
                };

                callVM.CallClosed += () => callWindow.Close();
                callWindow.Show();
            });
        }

        private void OnCallSignalReceived(CallSignalPacket signal)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                if (signal.Action == CallAction.Offer)
                {
                    var callVM = new CallViewModel(_network, _media);
                    callVM.InitializeIncoming(signal);

                    var callWindow = new CallWindow
                    {
                        DataContext = callVM,
                        Owner = App.Current?.MainWindow
                    };

                    callVM.CallClosed += () => callWindow.Close();
                    callWindow.Show();
                }
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
