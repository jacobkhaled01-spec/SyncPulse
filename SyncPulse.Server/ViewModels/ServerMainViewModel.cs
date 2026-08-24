using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SyncPulse.Core.Enums;
using SyncPulse.Server.Engine;
using SyncPulse.Server.Services;

namespace SyncPulse.Server.ViewModels
{
    public class ConnectedUserItem
    {
        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ClientIP { get; set; } = string.Empty;
        public int ClientPort { get; set; }
        public DateTime ConnectedAt { get; set; }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class ServerMainViewModel : INotifyPropertyChanged
    {
        private readonly TcpSocketServer _server;
        private bool _isRunning;
        private string _statusText = "الخادم متوقف 🔴";
        private string _serverIp = "127.0.0.1";
        private int _serverPort = 8888;
        private int _activeClientsCount = 0;

        public bool IsRunning
        {
            get => _isRunning;
            set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStart)); OnPropertyChanged(nameof(CanStop)); }
        }

        public bool CanStart => !IsRunning;
        public bool CanStop => IsRunning;

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public string ServerIP
        {
            get => _serverIp;
            set { _serverIp = value; OnPropertyChanged(); }
        }

        public int ServerPort
        {
            get => _serverPort;
            set { _serverPort = value; OnPropertyChanged(); }
        }

        public int ActiveClientsCount
        {
            get => _activeClientsCount;
            set { _activeClientsCount = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ConnectedUserItem> ConnectedClients { get; } = new();
        public ObservableCollection<string> PacketLogs { get; } = new();

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand KickCommand { get; }
        public ICommand ClearLogsCommand { get; }

        public ServerMainViewModel()
        {
            _server = new TcpSocketServer(8888);

            StartCommand = new RelayCommand(async _ => await StartServerAsync(), _ => CanStart);
            StopCommand = new RelayCommand(_ => StopServer(), _ => CanStop);
            KickCommand = new RelayCommand(param => KickClient(param));
            ClearLogsCommand = new RelayCommand(_ => PacketLogs.Clear());

            // ربط أحداث الخادم
            _server.StateChanged += running =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsRunning = running;
                    StatusText = running ? "الخادم يعمل بنشاط 🟢" : "الخادم متوقف 🔴";
                    ServerIP = _server.ServerIP;
                });
            };

            _server.LogMessageReceived += msg => AddLog(msg);

            _server.Sessions.ClientConnected += session =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ConnectedClients.Add(new ConnectedUserItem
                    {
                        UserID = session.UserID,
                        Username = session.Username,
                        DisplayName = session.DisplayName,
                        ClientIP = session.ClientIP,
                        ClientPort = session.ClientPort,
                        ConnectedAt = session.ConnectedAt
                    });
                    ActiveClientsCount = ConnectedClients.Count;
                });
            };

            _server.Sessions.ClientDisconnected += session =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var existing = ConnectedClients.FirstOrDefault(c => c.UserID == session.UserID);
                    if (existing != null)
                    {
                        ConnectedClients.Remove(existing);
                    }
                    ActiveClientsCount = ConnectedClients.Count;
                });
            };

            _server.Dispatcher.PacketProcessed += (session, type, length) =>
            {
                string user = string.IsNullOrEmpty(session.Username) ? session.ClientIP : session.Username;
                AddLog($"📦 [{DateTime.Now:HH:mm:ss}] حزمة {type} من {user} ({length} Bytes)");
            };
        }

        private async Task StartServerAsync()
        {
            try
            {
                await _server.StartAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل تشغيل الخادم: {ex.Message}", "خطأ في التشغيل", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopServer()
        {
            _server.Stop();
            ConnectedClients.Clear();
            ActiveClientsCount = 0;
        }

        private void KickClient(object? param)
        {
            if (param is ConnectedUserItem item)
            {
                _server.Sessions.KickUser(item.UserID);
                AddLog($"👢 تم طرد المستخدم: {item.Username} بواسطة مدير الخادم.");
            }
        }

        private void AddLog(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                PacketLogs.Insert(0, message);
                if (PacketLogs.Count > 300)
                {
                    PacketLogs.RemoveAt(PacketLogs.Count - 1);
                }
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
