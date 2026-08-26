using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SyncPulse.Core.Discovery;
using SyncPulse.Core.Protocol;
using SyncPulse.Core.Utils;
using SyncPulse.Server.Data;
using SyncPulse.Server.Services;

namespace SyncPulse.Server.Engine
{
    /// <summary>
    /// خادم المقابس المركزي متعدد الخيوط ومجمع الخدمات (Core TCP Socket Server)
    /// </summary>
    public class TcpSocketServer : IDisposable
    {
        public const int DefaultTcpPort = 8888;
        private readonly int _port;
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;

        public DatabaseManager Database { get; }
        public UserRepository Users { get; }
        public ContactRepository Contacts { get; }
        public MessageRepository Messages { get; }
        public CallRepository Calls { get; }
        public AuditLogRepository AuditLogs { get; }

        public SessionManager Sessions { get; }
        public CallCoordinator CallCoordinator { get; }
        public UdpMediaRelay MediaRelay { get; }
        public ServerDiscoveryBroadcaster DiscoveryBroadcaster { get; }
        public PacketDispatcher Dispatcher { get; }

        public bool IsRunning => _listener != null && _cts != null && !_cts.IsCancellationRequested;
        public string ServerIP { get; private set; } = "0.0.0.0";
        public int Port => _port;

        public event Action<bool>? StateChanged;
        public event Action<string>? LogMessageReceived;

        public TcpSocketServer(int port = DefaultTcpPort)
        {
            _port = port;

            // 1. تهيئة طبقة البيانات
            Database = new DatabaseManager();
            Users = new UserRepository(Database);
            Contacts = new ContactRepository(Database);
            Messages = new MessageRepository(Database);
            Calls = new CallRepository(Database);
            AuditLogs = new AuditLogRepository(Database);

            // 2. تهيئة طبقة الخدمات
            Sessions = new SessionManager(Users, AuditLogs);
            MediaRelay = new UdpMediaRelay();
            CallCoordinator = new CallCoordinator(Sessions, Calls, AuditLogs, MediaRelay);
            DiscoveryBroadcaster = new ServerDiscoveryBroadcaster();
            Dispatcher = new PacketDispatcher(Users, Contacts, Messages, Calls, Sessions, CallCoordinator, AuditLogs);
        }

        public async Task StartAsync()
        {
            if (IsRunning) return;

            // 1. تهيئة قاعدة البيانات SQLite
            await Database.InitializeDatabaseAsync();
            await AuditLogs.LogAsync("Info", "Server", "تمت تهيئة قاعدة البيانات والجداول بنجاح.");

            // 2. الاستماع على المقابس
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();

            ServerIP = NetworkUtils.GetPrimaryLocalIP();

            // 3. تشغيل مكرر تدفقات الوسائط UDP Relay
            MediaRelay.Start();

            // 4. تشغيل برودكاست الاكتشاف التلقائي لشبكات الواي فاي (UDP Port 8887)
            DiscoveryBroadcaster.StartBroadcasting(new ServerAnnouncement
            {
                ServerName = "SyncPulse Central Server",
                ServerIP = ServerIP,
                TcpPort = _port,
                UdpPort = UdpMediaRelay.DefaultMediaPort
            });

            LogMessageReceived?.Invoke($"🚀 تم تشغيل الخادم بنجاح على IP: {ServerIP} - المنفذ: {_port}");
            StateChanged?.Invoke(true);

            // 5. حلقة قبول اتصالات العملاء
            _ = AcceptClientsLoopAsync(_cts.Token);
        }

        private async Task AcceptClientsLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener != null)
            {
                try
                {
                    TcpClient tcpClient = await _listener.AcceptTcpClientAsync(cancellationToken);
                    _ = HandleClientConnectionAsync(tcpClient, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogMessageReceived?.Invoke($"⚠️ تنبيه مقبس الخادم: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// معالجة جلسة العميل مع عزل تام للاستثناءات (Bulkhead Fault-Tolerance Pattern)
        /// </summary>
        private async Task HandleClientConnectionAsync(TcpClient tcpClient, CancellationToken serverCts)
        {
            var session = new ClientSession(tcpClient);
            LogMessageReceived?.Invoke($"🔌 اتصال جديد من: {session.ClientIP}:{session.ClientPort}");

            try
            {
                using var clientCts = CancellationTokenSource.CreateLinkedTokenSource(serverCts);
                
                while (!clientCts.Token.IsCancellationRequested)
                {
                    // قراءة وتأطير الحزمة عبر State Machine
                    SyncPacket? packet = await FrameStreamParser.ReadPacketAsync(session.Stream, clientCts.Token);
                    if (packet == null)
                    {
                        // تم إغلاق الاتصال بأمان من طرف العميل
                        break;
                    }

                    // توجيه ومعالجة الحزمة
                    await Dispatcher.DispatchAsync(session, packet);
                }
            }
            catch (Exception ex)
            {
                // عزل الخطأ لمنع انهيار الخادم
                LogMessageReceived?.Invoke($"❌ انقطاع اتصال {session.Username} ({session.ClientIP}): {ex.Message}");
            }
            finally
            {
                if (session.IsAuthenticated)
                {
                    await Sessions.RemoveSessionAsync(session.UserID);
                }
                session.Dispose();
                LogMessageReceived?.Invoke($"🚪 تم إغلاق جلسة: {session.ClientIP}");
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                MediaRelay.Stop();
                DiscoveryBroadcaster.Stop();

                // إغلاق كافة الجلسات النشطة
                foreach (var session in Sessions.GetAllActiveSessions())
                {
                    session.Dispose();
                }

                LogMessageReceived?.Invoke("🛑 تم إيقاف خدمة الخادم بنجاح.");
            }
            finally
            {
                _listener = null;
                _cts = null;
                StateChanged?.Invoke(false);
            }
        }

        public void Dispose()
        {
            Stop();
            MediaRelay.Dispose();
            DiscoveryBroadcaster.Dispose();
        }
    }
}
