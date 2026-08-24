namespace SyncPulse.Core.Enums
{
    /// <summary>
    /// أنواع حزم بروتوكول SecureTalk / SyncPulse (16-bit Opcode)
    /// </summary>
    public enum PacketType : ushort
    {
        // 0x0000 - 0x00FF: حزم النظام والتحكم (System & Control)
        Heartbeat = 0x0001,
        HeartbeatAck = 0x0002,
        Disconnect = 0x0003,
        ServerDiscoveryQuery = 0x0004,
        ServerDiscoveryResponse = 0x0005,

        // 0x0100 - 0x01FF: حزم المصادقة والحسابات (Authentication & Accounts)
        RegisterRequest = 0x0101,
        RegisterResponse = 0x0102,
        LoginRequest = 0x0103,
        LoginResponse = 0x0104,
        LogoutRequest = 0x0105,
        UserProfileUpdate = 0x0106,

        // 0x0200 - 0x02FF: حزم المراسلة والمزامنة الفردية (Direct Messaging & Sync)
        DirectChatMessage = 0x0201,
        MessageDeliveryAck = 0x0202,
        MessageReadAck = 0x0203,
        SyncHistoryRequest = 0x0204,
        SyncHistoryResponse = 0x0205,
        TypingIndicator = 0x0206,

        // 0x0300 - 0x03FF: حزم جهات الاتصال والبحث (Contacts & Discovery)
        SearchUserRequest = 0x0301,
        SearchUserResponse = 0x0302,
        AddContactRequest = 0x0303,
        AddContactResponse = 0x0304,
        GetContactsListRequest = 0x0305,
        GetContactsListResponse = 0x0306,
        UserPresenceChanged = 0x0307,

        // 0x0400 - 0x04FF: حزم إشارات المكالمات الفردية (1-to-1 Call Signaling)
        CallOffer = 0x0401,
        CallRinging = 0x0402,
        CallAnswer = 0x0403,
        CallReject = 0x0404,
        CallBusy = 0x0405,
        CallEnd = 0x0406,

        // 0x0500 - 0x05FF: حزم بث وتدفق الوسائط (Media Streaming via UDP Relay)
        AudioFrame = 0x0501,
        VideoFrame = 0x0502,

        // 0x0600 - 0x06FF: أخطاء البروتوكول والإنذارات (Errors & Alerts)
        ProtocolError = 0x0601,
        AccessDenied = 0x0602
    }
}
