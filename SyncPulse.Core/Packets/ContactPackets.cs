using System;
using System.Collections.Generic;

namespace SyncPulse.Core.Packets
{
    public class UserContactItem
    {
        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public int ConversationID { get; set; }
        public string? LastMessageContent { get; set; }
        public DateTime? LastMessageTimestamp { get; set; }
        public int UnreadCount { get; set; }
    }

    public class SearchUserRequest
    {
        public string QueryUsername { get; set; } = string.Empty;
    }

    public class SearchUserResponse
    {
        public bool Found { get; set; }
        public string Message { get; set; } = string.Empty;
        public UserContactItem? User { get; set; }
    }

    public class AddContactRequest
    {
        public int OwnerUserID { get; set; }
        public int ContactUserID { get; set; }
        public string? CustomName { get; set; }
    }

    public class AddContactResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public UserContactItem? Contact { get; set; }
    }

    public class GetContactsListRequest
    {
        public int UserID { get; set; }
    }

    public class GetContactsListResponse
    {
        public int UserID { get; set; }
        public List<UserContactItem> Contacts { get; set; } = new();
    }

    public class UserPresenceChangedPacket
    {
        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
