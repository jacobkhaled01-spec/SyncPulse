using System;
using System.Collections.Generic;
using SyncPulse.Core.Enums;

namespace SyncPulse.Core.Packets
{
    /// <summary>
    /// حزمة إشارات التحكم في المكالمات الفردية (1-to-1 Call Signaling)
    /// </summary>
    public class CallSignalPacket
    {
        public int CallID { get; set; }
        public int CallerID { get; set; }
        public string CallerUsername { get; set; } = string.Empty;
        public string CallerDisplayName { get; set; } = string.Empty;
        public int ReceiverID { get; set; }
        public string ReceiverUsername { get; set; } = string.Empty;
        public CallAction Action { get; set; }
        public CallType Type { get; set; } = CallType.Audio;
        public int DurationSeconds { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// حزمة بث تدفقات الوسائط المباشرة للصوت والصورة عبر UDP Relay
    /// </summary>
    public class MediaFramePacket
    {
        public int CallID { get; set; }
        public int SenderID { get; set; }
        public CallType FrameType { get; set; }
        public uint SequenceNumber { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public byte[] FrameData { get; set; } = Array.Empty<byte>();
    }

    /// <summary>
    /// عنصر سجل المكالمة الفردية
    /// </summary>
    public class CallHistoryItem
    {
        public int CallID { get; set; }
        public int CallerID { get; set; }
        public string CallerName { get; set; } = string.Empty;
        public int ReceiverID { get; set; }
        public string ReceiverName { get; set; } = string.Empty;
        public CallType CallType { get; set; }
        public int DurationSeconds { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsOutgoing { get; set; }

        public string FormattedTime => Timestamp.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
        public string DurationFormatted => TimeSpan.FromSeconds(DurationSeconds).ToString(@"mm\:ss");
        public string TypeIcon => CallType == CallType.Video ? "📹" : "📞";
        public string DirectionIcon => IsOutgoing ? "↗️ صادرة" : "↙️ واردة";
        public string TargetPartyName => IsOutgoing ? ReceiverName : CallerName;
    }

    /// <summary>
    /// طلب جلب سجل المكالمات
    /// </summary>
    public class GetCallHistoryRequest
    {
        public int UserID { get; set; }
    }

    /// <summary>
    /// استجابة سجل المكالمات
    /// </summary>
    public class GetCallHistoryResponse
    {
        public int UserID { get; set; }
        public List<CallHistoryItem> Calls { get; set; } = new();
    }
}
