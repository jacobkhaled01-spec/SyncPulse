using System;

namespace SyncPulse.Client.Services
{
    public class ClientSessionState
    {
        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SessionToken { get; set; } = string.Empty;
        public bool IsAuthenticated => !string.IsNullOrEmpty(SessionToken) && UserID > 0;

        public string ServerIP { get; set; } = "127.0.0.1";
        public int ServerPort { get; set; } = 8888;
        public int UdpPort { get; set; } = 8889;

        public void Clear()
        {
            UserID = 0;
            Username = string.Empty;
            DisplayName = string.Empty;
            SessionToken = string.Empty;
        }
    }
}
