namespace SyncPulse.Core.Enums
{
    /// <summary>
    /// حالات الرسالة النصية الفردية (Telegram Style: Sent -> Delivered -> Read)
    /// </summary>
    public enum MessageStatus : byte
    {
        Sent = 0,      // صح واحدة (✓) - استلمها الخادم وخزنها
        Delivered = 1, // صحين (✓✓) - استلمها جهاز الطرف الآخر
        Read = 2       // صحين مقروءة (✓✓) - قرأها الطرف الآخر
    }

    /// <summary>
    /// نوع المكالمة الفردية
    /// </summary>
    public enum CallType : byte
    {
        Audio = 0,
        Video = 1
    }

    /// <summary>
    /// إجراءات وحالات إشارات المكالمة الفردية
    /// </summary>
    public enum CallAction : byte
    {
        Offer = 0,   // طلب بدء المكالمة
        Ringing = 1, // رنين لدى المستقبل
        Accept = 2,  // قبول المكالمة
        Reject = 3,  // رفض المكالمة
        Busy = 4,    // الخط مشغول في مكالمة أخرى
        End = 5,     // إنهاء المكالمة
        Offline = 6  // الطرف الآخر غير متصل بالشبكة حالياً
    }
}
