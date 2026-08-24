using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using SyncPulse.Client.Services;
using SyncPulse.Client.Utils;
using SyncPulse.Core.Discovery;
using SyncPulse.Core.Enums;
using SyncPulse.Core.Packets;

namespace SyncPulse.Client.ViewModels
{
    public class AuthViewModel : INotifyPropertyChanged
    {
        private readonly ClientNetworkService _network;
        private readonly ServerDiscoveryListener _discoveryListener;

        private string _serverIP = "127.0.0.1";
        private int _serverPort = 8888;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _displayName = string.Empty;
        private bool _isRegisterMode;
        private bool _isLoading;
        private string _errorMessage = string.Empty;
        private string _discoveryStatus = "جاري البحث التلقائي عن الخادم على شبكة الواي فاي...";

        public string ServerIP
        {
            get => _serverIP;
            set { _serverIP = value; OnPropertyChanged(); }
        }

        public int ServerPort
        {
            get => _serverPort;
            set { _serverPort = value; OnPropertyChanged(); }
        }

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); }
        }

        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }

        public bool IsRegisterMode
        {
            get => _isRegisterMode;
            set
            {
                _isRegisterMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActionTitle));
                OnPropertyChanged(nameof(SwitchModeText));
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrEmpty(_errorMessage);

        public string DiscoveryStatus
        {
            get => _discoveryStatus;
            set { _discoveryStatus = value; OnPropertyChanged(); }
        }

        public string ActionTitle => IsRegisterMode ? "إنشاء حساب جديد" : "تسجيل الدخول";
        public string SwitchModeText => IsRegisterMode ? "لديك حساب بالفعل؟ تسجيل الدخول" : "ليس لديك حساب؟ إنشاء حساب جديد الآن";

        public ICommand SubmitCommand { get; }
        public ICommand SwitchModeCommand { get; }

        public event Action? AuthenticatedSuccessfully;

        public AuthViewModel(ClientNetworkService network)
        {
            _network = network;
            _discoveryListener = new ServerDiscoveryListener();

            SubmitCommand = new RelayCommand(async () => await ExecuteSubmitAsync());
            SwitchModeCommand = new RelayCommand(() =>
            {
                IsRegisterMode = !IsRegisterMode;
                ErrorMessage = string.Empty;
            });

            StartAutoDiscovery();
        }

        private void StartAutoDiscovery()
        {
            _discoveryListener.ServerDiscovered += announcement =>
            {
                ServerIP = announcement.ServerIP;
                ServerPort = announcement.TcpPort;
                DiscoveryStatus = $"🟢 تم اكتشاف الخادم تلقائياً: {announcement.ServerName} ({announcement.ServerIP})";
            };

            try
            {
                _discoveryListener.StartListening();
            }
            catch
            {
                DiscoveryStatus = "الوضع اليدوي: أدخل عنوان IP الخادم";
            }
        }

        private async Task ExecuteSubmitAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "يرجى إدخال اسم المستخدم وكلمة المرور";
                return;
            }

            if (IsRegisterMode && string.IsNullOrWhiteSpace(DisplayName))
            {
                DisplayName = Username;
            }

            IsLoading = true;
            ErrorMessage = string.Empty;

            try
            {
                // 1. التأكد من الاتصال بالخادم
                if (!_network.IsConnected)
                {
                    bool connected = await _network.ConnectAsync(ServerIP, ServerPort);
                    if (!connected)
                    {
                        ErrorMessage = $"تعذر الاتصال بالخادم على {ServerIP}:{ServerPort}. تأكد من تشغيل الخادم.";
                        IsLoading = false;
                        return;
                    }
                }

                // 2. إرسال طلب التسجيل أو تسجيل الدخول
                if (IsRegisterMode)
                {
                    var regReq = new RegisterRequest
                    {
                        Username = Username.Trim(),
                        Password = Password,
                        DisplayName = DisplayName.Trim()
                    };

                    var regRes = await _network.SendRequestAsync<RegisterRequest, RegisterResponse>(
                        PacketType.RegisterRequest, regReq);

                    if (regRes != null && regRes.Success)
                    {
                        // بعد نجاح التسجيل، نقوم بتسجيل الدخول مباشرة
                        IsRegisterMode = false;
                        await ExecuteSubmitAsync();
                        return;
                    }
                    else
                    {
                        ErrorMessage = regRes?.Message ?? "فشل إنشاء الحساب. قد يكون اسم المستخدم محجوزاً.";
                    }
                }
                else
                {
                    var loginReq = new LoginRequest
                    {
                        Username = Username.Trim(),
                        Password = Password
                    };

                    var loginRes = await _network.SendRequestAsync<LoginRequest, LoginResponse>(
                        PacketType.LoginRequest, loginReq);

                    if (loginRes != null && loginRes.Success)
                    {
                        _network.Session.UserID = loginRes.UserID;
                        _network.Session.Username = loginRes.Username;
                        _network.Session.DisplayName = loginRes.DisplayName;
                        _network.Session.SessionToken = loginRes.SessionToken;

                        _discoveryListener.Stop();
                        AuthenticatedSuccessfully?.Invoke();
                    }
                    else
                    {
                        ErrorMessage = loginRes?.Message ?? "اسم المستخدم أو كلمة المرور غير صحيحة.";
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"حدث خطأ غير متوقع: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
