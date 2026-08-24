using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SyncPulse.Core.Enums;
using SyncPulse.Core.Packets;
using SyncPulse.Core.Protocol;
using SyncPulse.Server.Data;

namespace SyncPulse.Server.Services
{
    /// <summary>
    /// مدير الجلسات النشطة في الذاكرة الحية وتوزيع الإشعارات وحالة الحضور
    /// </summary>
    public class SessionManager
    {
        private readonly ConcurrentDictionary<int, ClientSession> _activeSessions = new();
        private readonly UserRepository _userRepo;
        private readonly AuditLogRepository _auditRepo;

        public event Action<ClientSession>? ClientConnected;
        public event Action<ClientSession>? ClientDisconnected;
        public event Action<int>? ActiveCountChanged;

        public SessionManager(UserRepository userRepo, AuditLogRepository auditRepo)
        {
            _userRepo = userRepo;
            _auditRepo = auditRepo;
        }

        public int ActiveCount => _activeSessions.Count;

        public bool TryGetSession(int userId, out ClientSession session)
        {
            return _activeSessions.TryGetValue(userId, out session!);
        }

        public async Task RegisterAuthenticatedSessionAsync(int userId, ClientSession session)
        {
            // إذا كان هناك اتصال قديم مختلف للمستخدم، نغلقه بلطف
            if (_activeSessions.TryRemove(userId, out var oldSession) && oldSession != session)
            {
                try { oldSession.Close(); } catch { }
            }

            _activeSessions[userId] = session;
            ClientConnected?.Invoke(session);
            ActiveCountChanged?.Invoke(_activeSessions.Count);

            await _auditRepo.LogAsync("Info", "Auth", $"تسجيل دخول ناجح للمستخدم: {session.Username}", userId, session.ClientIP);
            await BroadcastPresenceAsync(userId, session.Username, true);
        }

        public async Task RemoveSessionAsync(int userId)
        {
            if (_activeSessions.TryRemove(userId, out var session))
            {
                ClientDisconnected?.Invoke(session);
                ActiveCountChanged?.Invoke(_activeSessions.Count);

                await _userRepo.UpdateLastSeenAsync(userId);
                await _auditRepo.LogAsync("Info", "Auth", $"تسجيل خروج للمستخدم: {session.Username}", userId, session.ClientIP);
                await BroadcastPresenceAsync(userId, session.Username, false);
            }
        }

        public async Task BroadcastPresenceAsync(int userId, string username, bool isOnline)
        {
            var packet = SyncPacket.Create(PacketType.UserPresenceChanged, new UserPresenceChangedPacket
            {
                UserID = userId,
                Username = username,
                IsOnline = isOnline,
                Timestamp = DateTime.UtcNow
            });

            var tasks = _activeSessions.Values
                .Where(s => s.UserID != userId)
                .Select(s => s.SendPacketAsync(packet));

            await Task.WhenAll(tasks);
        }

        public List<ClientSession> GetAllActiveSessions()
        {
            return _activeSessions.Values.ToList();
        }

        public async Task BroadcastSystemNotificationAsync(string announcementText)
        {
            var packet = SyncPacket.Create(PacketType.DirectChatMessage, new ChatMessagePacket
            {
                MessageID = 0,
                SenderID = 0,
                SenderUsername = "📢 SYSTEM BROADCAST",
                Content = announcementText,
                Timestamp = DateTime.UtcNow,
                Status = MessageStatus.Delivered
            });

            var tasks = _activeSessions.Values.Select(s => s.SendPacketAsync(packet));
            await Task.WhenAll(tasks);
        }

        public void KickUser(int userId)
        {
            if (_activeSessions.TryGetValue(userId, out var session))
            {
                session.Close();
                _activeSessions.TryRemove(userId, out _);
                ClientDisconnected?.Invoke(session);
                ActiveCountChanged?.Invoke(_activeSessions.Count);
            }
        }
    }
}
