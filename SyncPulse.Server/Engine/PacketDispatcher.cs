using System;
using System.Threading.Tasks;
using SyncPulse.Core.Enums;
using SyncPulse.Core.Packets;
using SyncPulse.Core.Protocol;
using SyncPulse.Core.Security;
using SyncPulse.Server.Data;
using SyncPulse.Server.Services;

namespace SyncPulse.Server.Engine
{
    /// <summary>
    /// موجه الحزم المركزي ومنفذ منطق الأعمال (Central Packet Dispatcher)
    /// </summary>
    public class PacketDispatcher
    {
        private readonly UserRepository _userRepo;
        private readonly ContactRepository _contactRepo;
        private readonly MessageRepository _messageRepo;
        private readonly CallRepository _callRepo;
        private readonly SessionManager _sessionManager;
        private readonly CallCoordinator _callCoordinator;
        private readonly AuditLogRepository _auditRepo;

        public event Action<ClientSession, PacketType, int>? PacketProcessed; // (Session, Type, Length)

        public PacketDispatcher(
            UserRepository userRepo,
            ContactRepository contactRepo,
            MessageRepository messageRepo,
            CallRepository callRepo,
            SessionManager sessionManager,
            CallCoordinator callCoordinator,
            AuditLogRepository auditRepo)
        {
            _userRepo = userRepo;
            _contactRepo = contactRepo;
            _messageRepo = messageRepo;
            _callRepo = callRepo;
            _sessionManager = sessionManager;
            _callCoordinator = callCoordinator;
            _auditRepo = auditRepo;
        }

        public async Task DispatchAsync(ClientSession session, SyncPacket packet)
        {
            PacketProcessed?.Invoke(session, packet.Header.Type, packet.Payload.Length);

            switch (packet.Header.Type)
            {
                case PacketType.Heartbeat:
                    await session.SendPacketAsync(new SyncPacket(PacketType.HeartbeatAck, Array.Empty<byte>()));
                    break;

                case PacketType.RegisterRequest:
                    await HandleRegisterAsync(session, packet);
                    break;

                case PacketType.LoginRequest:
                    await HandleLoginAsync(session, packet);
                    break;

                case PacketType.DirectChatMessage:
                    await HandleChatMessageAsync(session, packet);
                    break;

                case PacketType.MessageDeliveryAck:
                case PacketType.MessageReadAck:
                    await HandleMessageAckAsync(session, packet);
                    break;

                case PacketType.SyncHistoryRequest:
                    await HandleSyncHistoryAsync(session, packet);
                    break;

                case PacketType.SearchUserRequest:
                    await HandleSearchUserAsync(session, packet);
                    break;

                case PacketType.AddContactRequest:
                    await HandleAddContactAsync(session, packet);
                    break;

                case PacketType.GetContactsListRequest:
                    await HandleGetContactsListAsync(session, packet);
                    break;

                case PacketType.CallOffer:
                case PacketType.CallRinging:
                case PacketType.CallAnswer:
                case PacketType.CallReject:
                case PacketType.CallBusy:
                case PacketType.CallEnd:
                    var callSignal = packet.GetPayload<CallSignalPacket>();
                    if (callSignal != null)
                    {
                        await _callCoordinator.HandleCallSignalAsync(session, callSignal);
                    }
                    break;

                case PacketType.TypingIndicator:
                    await HandleTypingIndicatorAsync(session, packet);
                    break;

                case PacketType.GetCallHistoryRequest:
                    await HandleGetCallHistoryAsync(session, packet);
                    break;

                case PacketType.ClearChatHistoryRequest:
                    await HandleClearChatHistoryAsync(session, packet);
                    break;
            }
        }

        private async Task HandleRegisterAsync(ClientSession session, SyncPacket packet)
        {
            var req = packet.GetPayload<RegisterRequest>();
            if (req == null) return;

            var (success, userId, message) = await _userRepo.RegisterUserAsync(req.Username, req.Password, req.DisplayName);
            var res = new RegisterResponse
            {
                Success = success,
                Message = message,
                UserID = userId
            };

            await session.SendPacketAsync(SyncPacket.Create(PacketType.RegisterResponse, res, packet.Header.SequenceNumber));
            if (success)
            {
                await _auditRepo.LogAsync("Info", "Auth", $"تسجيل مستخدم جديد: {req.Username}", userId, session.ClientIP);
            }
        }

        private async Task HandleLoginAsync(ClientSession session, SyncPacket packet)
        {
            var req = packet.GetPayload<LoginRequest>();
            if (req == null) return;

            var (success, userId, username, displayName, message) = await _userRepo.AuthenticateUserAsync(req.Username, req.Password);
            if (!success)
            {
                await session.SendPacketAsync(SyncPacket.Create(PacketType.LoginResponse, new LoginResponse
                {
                    Success = false,
                    Message = message
                }, packet.Header.SequenceNumber));
                return;
            }

            session.UserID = userId;
            session.Username = username;
            session.DisplayName = displayName;

            string token = JwtTokenEngine.GenerateToken(userId, username, TimeSpan.FromDays(7));
            await _sessionManager.RegisterAuthenticatedSessionAsync(userId, session);

            // إرسال رد تسجيل الدخول الناجح مع نفس رقم التسلسل
            await session.SendPacketAsync(SyncPacket.Create(PacketType.LoginResponse, new LoginResponse
            {
                Success = true,
                Message = message,
                UserID = userId,
                Username = username,
                DisplayName = displayName,
                SessionToken = token
            }, packet.Header.SequenceNumber));

            // مرحلة المزامنة التلغرامية الفورية: دفع الرسائل المعلقة
            await DeliverPendingOfflineMessagesAsync(session);
        }

        private async Task DeliverPendingOfflineMessagesAsync(ClientSession session)
        {
            var pendingMessages = await _messageRepo.GetUndeliveredMessagesAsync(session.UserID);
            foreach (var msg in pendingMessages)
            {
                // إرسال الرسالة للعميل المتصل حديثاً
                await session.SendPacketAsync(SyncPacket.Create(PacketType.DirectChatMessage, msg));
                
                // تحديث الحالة إلى Delivered
                await _messageRepo.UpdateMessageStatusAsync(msg.MessageID, MessageStatus.Delivered);

                // إشعار المرسل الأصلي إذا كان متصلاً بأن رسالته تم تسليمها (علامتي صح ✓✓)
                if (_sessionManager.TryGetSession(msg.SenderID, out var senderSession))
                {
                    await senderSession.SendPacketAsync(SyncPacket.Create(PacketType.MessageDeliveryAck, new MessageAckPacket
                    {
                        MessageID = msg.MessageID,
                        ConversationID = msg.ConversationID,
                        SenderID = msg.SenderID,
                        ReceiverID = session.UserID,
                        NewStatus = MessageStatus.Delivered,
                        AcknowledgedAt = DateTime.UtcNow
                    }));
                }
            }
        }

        private async Task HandleChatMessageAsync(ClientSession session, SyncPacket packet)
        {
            var msg = packet.GetPayload<ChatMessagePacket>();
            if (msg == null || !session.IsAuthenticated) return;

            // 1. إنشاء أو جلب المحادثة
            int convId = await _messageRepo.GetOrCreateConversationAsync(session.UserID, msg.ReceiverID);
            msg.ConversationID = convId;
            msg.SenderID = session.UserID;
            msg.SenderUsername = session.Username;
            msg.Timestamp = DateTime.UtcNow;
            msg.Status = MessageStatus.Sent;

            // 2. حفظ الرسالة في قاعدة البيانات المركزية
            int msgId = await _messageRepo.SaveMessageAsync(convId, msg.SenderID, msg.ReceiverID, msg.Content, msg.AttachmentPath);
            msg.MessageID = msgId;

            // 3. تأكيد استلام الخادم للمرسل (صح واحدة ✓)
            await session.SendPacketAsync(SyncPacket.Create(PacketType.MessageDeliveryAck, new MessageAckPacket
            {
                MessageID = msgId,
                ConversationID = convId,
                SenderID = msg.SenderID,
                ReceiverID = msg.ReceiverID,
                NewStatus = MessageStatus.Sent,
                AcknowledgedAt = DateTime.UtcNow
            }));

            // 4. فحص هل المستلم متصل بالخادم الآن؟
            if (_sessionManager.TryGetSession(msg.ReceiverID, out var receiverSession))
            {
                // المستلم متصل -> تمرير الرسالة فوراً
                await receiverSession.SendPacketAsync(SyncPacket.Create(PacketType.DirectChatMessage, msg));

                // إشعار فوري للمرسل بأنها استُلمت في جهاز الطرف الآخر (✓✓)
                await session.SendPacketAsync(SyncPacket.Create(PacketType.MessageDeliveryAck, new MessageAckPacket
                {
                    MessageID = msgId,
                    ConversationID = convId,
                    SenderID = msg.SenderID,
                    ReceiverID = msg.ReceiverID,
                    NewStatus = MessageStatus.Delivered,
                    AcknowledgedAt = DateTime.UtcNow
                }));
                await _messageRepo.UpdateMessageStatusAsync(msgId, MessageStatus.Delivered);
            }
            // في حال عدم اتصال المستلم: تبقى الرسالة في قاعدة البيانات وتُدفع تلقائياً عند اتصاله
        }

        private async Task HandleMessageAckAsync(ClientSession session, SyncPacket packet)
        {
            var ack = packet.GetPayload<MessageAckPacket>();
            if (ack == null) return;

            // تحديث الحالة في قاعدة البيانات
            await _messageRepo.UpdateMessageStatusAsync(ack.MessageID, ack.NewStatus);

            // تمرير إشعار التحديث للطرف الآخر (المرسل الأصلي أو المستلم)
            int targetUserId = (session.UserID == ack.SenderID) ? ack.ReceiverID : ack.SenderID;
            if (_sessionManager.TryGetSession(targetUserId, out var targetSession))
            {
                await targetSession.SendPacketAsync(packet);
            }
        }

        private async Task HandleClearChatHistoryAsync(ClientSession session, SyncPacket packet)
        {
            var req = packet.GetPayload<ClearChatHistoryPacket>();
            if (req == null || !session.IsAuthenticated) return;

            await _messageRepo.ClearConversationMessagesAsync(session.UserID, req.TargetUserID);
        }

        private async Task HandleSyncHistoryAsync(ClientSession session, SyncPacket packet)
        {
            var req = packet.GetPayload<SyncHistoryRequestPacket>();
            if (req == null || !session.IsAuthenticated) return;

            // جلب تاريخ المحادثات والرسائل الكامل
            var allMessages = await _messageRepo.GetAllUserMessagesAsync(session.UserID);
            var response = new SyncHistoryResponsePacket
            {
                UserID = session.UserID,
                Messages = allMessages,
                UndeliveredCount = 0
            };

            await session.SendPacketAsync(SyncPacket.Create(PacketType.SyncHistoryResponse, response, packet.Header.SequenceNumber));
        }

        private async Task HandleSearchUserAsync(ClientSession session, SyncPacket packet)
        {
            var req = packet.GetPayload<SearchUserRequest>();
            if (req == null) return;

            var user = await _userRepo.SearchUserByUsernameAsync(req.QueryUsername);
            if (user != null)
            {
                user.IsOnline = _sessionManager.TryGetSession(user.UserID, out _);
                int convId = await _messageRepo.GetOrCreateConversationAsync(session.UserID, user.UserID);
                user.ConversationID = convId;
            }

            var response = new SearchUserResponse
            {
                Found = user != null,
                Message = user != null ? "تم العثور على المستخدم" : "لم يتم العثور على أي مستخدم بهذا الاسم",
                User = user
            };

            await session.SendPacketAsync(SyncPacket.Create(PacketType.SearchUserResponse, response, packet.Header.SequenceNumber));
        }

        private async Task HandleAddContactAsync(ClientSession session, SyncPacket packet)
        {
            var req = packet.GetPayload<AddContactRequest>();
            if (req == null || !session.IsAuthenticated) return;

            req.OwnerUserID = session.UserID;
            bool success = await _contactRepo.AddContactAsync(req.OwnerUserID, req.ContactUserID, req.CustomName);

            var contactDetails = await _userRepo.SearchUserByUsernameAsync(req.CustomName ?? "");
            if (contactDetails != null)
            {
                contactDetails.IsOnline = _sessionManager.TryGetSession(contactDetails.UserID, out _);
            }

            await session.SendPacketAsync(SyncPacket.Create(PacketType.AddContactResponse, new AddContactResponse
            {
                Success = success,
                Message = success ? "تمت إضافة جهة الاتصال بنجاح." : "فشلت عملية الإضافة.",
                Contact = contactDetails
            }, packet.Header.SequenceNumber));
        }

        private async Task HandleGetContactsListAsync(ClientSession session, SyncPacket packet)
        {
            if (!session.IsAuthenticated) return;

            var contacts = await _contactRepo.GetContactsAsync(session.UserID);
            foreach (var contact in contacts)
            {
                contact.IsOnline = _sessionManager.TryGetSession(contact.UserID, out _);
                contact.ConversationID = await _messageRepo.GetOrCreateConversationAsync(session.UserID, contact.UserID);
            }

            await session.SendPacketAsync(SyncPacket.Create(PacketType.GetContactsListResponse, new GetContactsListResponse
            {
                UserID = session.UserID,
                Contacts = contacts
            }, packet.Header.SequenceNumber));
        }

        private async Task HandleTypingIndicatorAsync(ClientSession session, SyncPacket packet)
        {
            var typing = packet.GetPayload<TypingIndicatorPacket>();
            if (typing == null || !session.IsAuthenticated) return;

            typing.SenderID = session.UserID;
            typing.SenderUsername = session.Username;

            if (_sessionManager.TryGetSession(typing.ReceiverID, out var receiverSession))
            {
                await receiverSession.SendPacketAsync(packet);
            }
        }

        private async Task HandleGetCallHistoryAsync(ClientSession session, SyncPacket packet)
        {
            if (!session.IsAuthenticated) return;

            var calls = await _callRepo.GetUserCallHistoryAsync(session.UserID);
            var response = new GetCallHistoryResponse
            {
                UserID = session.UserID,
                Calls = calls
            };

            await session.SendPacketAsync(SyncPacket.Create(PacketType.GetCallHistoryResponse, response, packet.Header.SequenceNumber));
        }
    }
}
