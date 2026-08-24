using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SyncPulse.Core.Enums;
using SyncPulse.Core.Packets;
using SyncPulse.Server.Data;
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
        private int _totalUsersCount = 0;
        private int _totalMessagesCount = 0;
        private int _totalCallsCount = 0;
        private string _broadcastMessageText = string.Empty;

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

        public int TotalUsersCount
        {
            get => _totalUsersCount;
            set { _totalUsersCount = value; OnPropertyChanged(); }
        }

        public int TotalMessagesCount
        {
            get => _totalMessagesCount;
            set { _totalMessagesCount = value; OnPropertyChanged(); }
        }

        public int TotalCallsCount
        {
            get => _totalCallsCount;
            set { _totalCallsCount = value; OnPropertyChanged(); }
        }

        public string BroadcastMessageText
        {
            get => _broadcastMessageText;
            set { _broadcastMessageText = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ConnectedUserItem> ConnectedClients { get; } = new();
        public ObservableCollection<UserContactItem> RegisteredUsers { get; } = new();
        public ObservableCollection<CallRecordItem> CallHistoryLogs { get; } = new();
        public ObservableCollection<AuditLogEntry> AuditLogsList { get; } = new();
        public ObservableCollection<string> PacketLogs { get; } = new();

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand KickCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand SendBroadcastCommand { get; }
        public ICommand RefreshDataCommand { get; }
        public ICommand BanUserCommand { get; }
        public ICommand UnbanUserCommand { get; }
        public ICommand ResetPasswordCommand { get; }

        public ServerMainViewModel()
        {
            _server = new TcpSocketServer(8888);

            StartCommand = new RelayCommand(async _ => await StartServerAsync(), _ => CanStart);
            StopCommand = new RelayCommand(_ => StopServer(), _ => CanStop);
            KickCommand = new RelayCommand(param => KickClient(param));
            ClearLogsCommand = new RelayCommand(_ => PacketLogs.Clear());
            SendBroadcastCommand = new RelayCommand(async _ => await SendBroadcastAsync());
            RefreshDataCommand = new RelayCommand(async _ => await RefreshAllDataAsync());
            BanUserCommand = new RelayCommand(async param => await SetUserBanStatusAsync(param, false));
            UnbanUserCommand = new RelayCommand(async param => await SetUserBanStatusAsync(param, true));
            ResetPasswordCommand = new RelayCommand(async param => await ResetPasswordAsync(param));

            // ربط أحداث الخادم
            _server.StateChanged += running =>
            {
                Application.Current.Dispatcher.Invoke(async () =>
                {
                    IsRunning = running;
                    StatusText = running ? "الخادم يعمل بنشاط 🟢" : "الخادم متوقف 🔴";
                    ServerIP = _server.ServerIP;
                    if (running)
                    {
                        await RefreshAllDataAsync();
                    }
                });
            };

            _server.LogMessageReceived += msg => AddLog(msg);

            _server.Sessions.ClientConnected += session =>
            {
                Application.Current.Dispatcher.Invoke(async () =>
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
                    await RefreshAllDataAsync();
                });
            };

            _server.Sessions.ClientDisconnected += session =>
            {
                Application.Current.Dispatcher.Invoke(async () =>
                {
                    var existing = ConnectedClients.FirstOrDefault(c => c.UserID == session.UserID);
                    if (existing != null)
                    {
                        ConnectedClients.Remove(existing);
                    }
                    ActiveClientsCount = ConnectedClients.Count;
                    await RefreshAllDataAsync();
                });
            };

            _server.Dispatcher.PacketProcessed += (session, type, length) =>
            {
                string user = string.IsNullOrEmpty(session.Username) ? session.ClientIP : session.Username;
                AddLog($"📦 [{DateTime.Now:HH:mm:ss}] {type} من {user} ({length} Bytes)");
            };
        }

        private async Task StartServerAsync()
        {
            try
            {
                await _server.StartAsync();
                await RefreshAllDataAsync();
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

        private async Task SendBroadcastAsync()
        {
            if (string.IsNullOrWhiteSpace(BroadcastMessageText) || !IsRunning) return;

            string msg = BroadcastMessageText.Trim();
            await _server.Sessions.BroadcastSystemNotificationAsync(msg);
            await _server.AuditLogs.LogAsync("Info", "Broadcast", $"تنبيه عام: {msg}");
            AddLog($"📢 تم بث إشعار عام: {msg}");
            BroadcastMessageText = string.Empty;
        }

        public async Task RefreshAllDataAsync()
        {
            try
            {
                TotalUsersCount = await _server.Users.GetTotalUsersCountAsync();
                TotalMessagesCount = await _server.Messages.GetTotalMessagesCountAsync();
                TotalCallsCount = await _server.Calls.GetTotalCallsCountAsync();

                // تحديث المستخدمين
                var users = await _server.Users.GetAllUsersWithStatusAsync();
                RegisteredUsers.Clear();
                foreach (var u in users) RegisteredUsers.Add(u);

                // تحديث سجل المكالمات
                var calls = await _server.Calls.GetRecentCallsAsync(50);
                CallHistoryLogs.Clear();
                foreach (var c in calls) CallHistoryLogs.Add(c);

                // تحديث سجلات التدقيق
                var logs = await _server.AuditLogs.GetRecentLogsAsync(100);
                AuditLogsList.Clear();
                foreach (var l in logs) AuditLogsList.Add(l);
            }
            catch
            {
                // Fallback gracefully
            }
        }

        private async Task SetUserBanStatusAsync(object? param, bool isActive)
        {
            if (param is UserContactItem user)
            {
                bool success = await _server.Users.SetUserActiveStatusAsync(user.UserID, isActive);
                if (success)
                {
                    if (!isActive)
                    {
                        _server.Sessions.KickUser(user.UserID);
                    }
                    await _server.AuditLogs.LogAsync("Warning", "Admin", $"تعديل حالة الحساب: {user.Username} -> {(isActive ? "نشط" : "محظور")}");
                    AddLog($"🛡️ تم {(isActive ? "فك حظر" : "حظر")} المستخدم: {user.Username}");
                    await RefreshAllDataAsync();
                }
            }
        }

        private async Task ResetPasswordAsync(object? param)
        {
            if (param is UserContactItem user)
            {
                string defaultNewPass = "SyncPulse123!";
                bool success = await _server.Users.ResetUserPasswordAsync(user.UserID, defaultNewPass);
                if (success)
                {
                    await _server.AuditLogs.LogAsync("Warning", "Admin", $"إعادة تعيين كلمة مرور المستخدم: {user.Username}");
                    MessageBox.Show($"تمت إعادة تعيين كلمة مرور {user.Username} إلى:\n{defaultNewPass}", "إعادة تعيين كلمة المرور", MessageBoxButton.OK, MessageBoxImage.Information);
                    AddLog($"🔑 تم إعادة تعيين كلمة مرور {user.Username}");
                }
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
