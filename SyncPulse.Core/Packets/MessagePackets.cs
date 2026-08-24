using System;
using System.Collections.Generic;
using SyncPulse.Core.Enums;

namespace SyncPulse.Core.Packets
{
    /// <summary>
    /// حزمة الرسالة النصية الفردية
    /// </summary>
    public class ChatMessagePacket
    {
        public int MessageID { get; set; }
        public int ConversationID { get; set; }
        public int SenderID { get; set; }
        public string SenderUsername { get; set; } = string.Empty;
        public int ReceiverID { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? AttachmentPath { get; set; }
        public string? AttachmentFileName { get; set; }
        public byte[]? AttachmentData { get; set; }
        public long AttachmentSize { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public MessageStatus Status { get; set; } = MessageStatus.Sent;
    }

    /// <summary>
    /// حزمة إشعار استلام وتأكيد وصول الرسالة (ACK)
    /// </summary>
    public class MessageAckPacket
    {
        public int MessageID { get; set; }
        public int ConversationID { get; set; }
        public int SenderID { get; set; }
        public int ReceiverID { get; set; }
        public MessageStatus NewStatus { get; set; }
        public DateTime AcknowledgedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// حزمة طلب مزامنة الرسائل غير المستلمة وتاريخ المحادثة (Telegram Sync)
    /// </summary>
    public class SyncHistoryRequestPacket
    {
        public int UserID { get; set; }
        public int LastKnownMessageID { get; set; }
    }

    /// <summary>
    /// حزمة الاستجابة بالمزامنة وقائمة الرسائل المتراكمة
    /// </summary>
    public class SyncHistoryResponsePacket
    {
        public int UserID { get; set; }
        public List<ChatMessagePacket> Messages { get; set; } = new();
        public int UndeliveredCount { get; set; }
    }

    /// <summary>
    /// مؤشر جاري الكتابة اللحظي (Typing...)
    /// </summary>
    public class TypingIndicatorPacket
    {
        public int SenderID { get; set; }
        public string SenderUsername { get; set; } = string.Empty;
        public int ReceiverID { get; set; }
        public bool IsTyping { get; set; }
    }

    /// <summary>
    /// حزمة طلب مسح سجل المحادثة الفردية نهائياً من قاعدة البيانات
    /// </summary>
    public class ClearChatHistoryPacket
    {
        public int UserID { get; set; }
        public int TargetUserID { get; set; }
    }
}
