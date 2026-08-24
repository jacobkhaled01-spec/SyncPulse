using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using SyncPulse.Core.Enums;
using SyncPulse.Core.Packets;
using SyncPulse.Core.Protocol;
using SyncPulse.Server.Data;

namespace SyncPulse.Server.Services
{
    public class ActiveCallState
    {
        public int CallID { get; set; }
        public int CallerID { get; set; }
        public int ReceiverID { get; set; }
        public CallType Type { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConnectedAt { get; set; }
    }

    /// <summary>
    /// منسق إشارات المكالمات الفردية وتتبع حالة الاتصال
    /// </summary>
    public class CallCoordinator
    {
        private readonly ConcurrentDictionary<int, ActiveCallState> _activeCalls = new();
        private readonly SessionManager _sessionManager;
        private readonly CallRepository _callRepo;
        private readonly AuditLogRepository _auditRepo;

        public CallCoordinator(SessionManager sessionManager, CallRepository callRepo, AuditLogRepository auditRepo)
        {
            _sessionManager = sessionManager;
            _callRepo = callRepo;
            _auditRepo = auditRepo;
        }

        public async Task HandleCallSignalAsync(ClientSession senderSession, CallSignalPacket signal)
        {
            switch (signal.Action)
            {
                case CallAction.Offer:
                    await HandleCallOfferAsync(senderSession, signal);
                    break;

                case CallAction.Ringing:
                    await RelaySignalAsync(signal.CallerID, signal);
                    break;

                case CallAction.Accept:
                    await HandleCallAcceptAsync(senderSession, signal);
                    break;

                case CallAction.Reject:
                    await HandleCallRejectAsync(senderSession, signal);
                    break;

                case CallAction.Busy:
                    await RelaySignalAsync(signal.CallerID, signal);
                    break;

                case CallAction.End:
                    await HandleCallEndAsync(senderSession, signal);
                    break;
            }
        }

        private async Task HandleCallOfferAsync(ClientSession callerSession, CallSignalPacket signal)
        {
            // 1. فحص هل الطرف المستهدف متصل
            if (!_sessionManager.TryGetSession(signal.ReceiverID, out var receiverSession))
            {
                // الطرف الآخر غير متصل
                signal.Action = CallAction.Offline;
                await callerSession.SendPacketAsync(SyncPacket.Create(PacketType.CallBusy, signal));
                int offlineCallId = await _callRepo.LogCallStartAsync(signal.CallerID, signal.ReceiverID, signal.Type);
                await _callRepo.EndCallAsync(offlineCallId, 0, "Missed/Offline");
                return;
            }

            // 2. تسجيل بدء طلب المكالمة في قاعدة البيانات
            signal.CallerDisplayName = string.IsNullOrEmpty(signal.CallerDisplayName) ? callerSession.DisplayName : signal.CallerDisplayName;
            signal.CallerUsername = string.IsNullOrEmpty(signal.CallerUsername) ? callerSession.Username : signal.CallerUsername;

            int callId = await _callRepo.LogCallStartAsync(signal.CallerID, signal.ReceiverID, signal.Type);
            signal.CallID = callId;

            var callState = new ActiveCallState
            {
                CallID = callId,
                CallerID = signal.CallerID,
                ReceiverID = signal.ReceiverID,
                Type = signal.Type
            };
            _activeCalls[callId] = callState;

            // 3. تمرير طلب الرنين للطرف الآخر
            await receiverSession.SendPacketAsync(SyncPacket.Create(PacketType.CallOffer, signal));
            await _auditRepo.LogAsync("Info", "Call", $"بدء مكالمة {signal.Type} بين {signal.CallerUsername} و {signal.ReceiverUsername}", signal.CallerID);
        }

        private async Task HandleCallAcceptAsync(ClientSession receiverSession, CallSignalPacket signal)
        {
            if (_activeCalls.TryGetValue(signal.CallID, out var callState))
            {
                callState.ConnectedAt = DateTime.UtcNow;
            }

            // إشعار المتصل بأن المكالمة قُبلت
            signal.Action = CallAction.Accept;
            await RelaySignalAsync(signal.CallerID, signal);
        }

        private async Task HandleCallRejectAsync(ClientSession session, CallSignalPacket signal)
        {
            if (_activeCalls.TryRemove(signal.CallID, out _))
            {
                await _callRepo.EndCallAsync(signal.CallID, 0, "Rejected");
            }
            await RelaySignalAsync(signal.CallerID, signal);
        }

        private async Task HandleCallEndAsync(ClientSession session, CallSignalPacket signal)
        {
            int duration = 0;
            if (_activeCalls.TryRemove(signal.CallID, out var callState))
            {
                if (callState.ConnectedAt.HasValue)
                {
                    duration = (int)(DateTime.UtcNow - callState.ConnectedAt.Value).TotalSeconds;
                }
                await _callRepo.EndCallAsync(signal.CallID, duration, "Completed");
            }

            signal.DurationSeconds = duration;

            // إشعار كلا الطرفين بانتهاء المكالمة
            int peerId = (session.UserID == signal.CallerID) ? signal.ReceiverID : signal.CallerID;
            await RelaySignalAsync(peerId, signal);
        }

        private async Task RelaySignalAsync(int targetUserId, CallSignalPacket signal)
        {
            if (_sessionManager.TryGetSession(targetUserId, out var targetSession))
            {
                PacketType packetType = signal.Action switch
                {
                    CallAction.Offer => PacketType.CallOffer,
                    CallAction.Ringing => PacketType.CallRinging,
                    CallAction.Accept => PacketType.CallAnswer,
                    CallAction.Reject => PacketType.CallReject,
                    CallAction.Busy => PacketType.CallBusy,
                    CallAction.End => PacketType.CallEnd,
                    _ => PacketType.CallOffer
                };

                await targetSession.SendPacketAsync(SyncPacket.Create(packetType, signal));
            }
        }
    }
}
