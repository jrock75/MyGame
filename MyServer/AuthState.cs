using System.Net;

namespace MyServer
{
    public static class AuthState
    {
        // sessionKey → username
        public static readonly Dictionary<Guid, string> ActiveSessions = new();

        // sessionKey → playerId (UDP only)
        public static readonly Dictionary<Guid, int> SessionToPlayer = new();
    }
}