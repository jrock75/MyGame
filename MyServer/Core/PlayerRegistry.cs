using MyGame.Shared;
using System;
using System.Collections.Generic;
using System.Net;

namespace MyGame.MyServer.Core
{
    public sealed class PlayerRegistry
    {
        private readonly Dictionary<Guid, ClientConnection> connections = new();
        private readonly Dictionary<IPEndPoint, Guid> endpointToId = new();

        public int Count => connections.Count;

        public bool TryGetIdByEndpoint(IPEndPoint ep, out Guid id) => endpointToId.TryGetValue(ep, out id);

        public bool TryGetConnection(Guid id, out ClientConnection? conn) => connections.TryGetValue(id, out conn);

        public IEnumerable<ClientConnection> Connections => connections.Values;

        public Guid GetOrCreatePlayer(IPEndPoint sender, long nowMs, out bool wasExisting)
        {
            if (endpointToId.TryGetValue(sender, out var existingId) &&
                connections.TryGetValue(existingId, out var existingConn))
            {
                existingConn.LastHeardMs = nowMs;
                wasExisting = true;
                return existingId;
            }

            // clean stale mapping
            endpointToId.Remove(sender);

            var newId = Guid.NewGuid();
            var state = new PlayerState { Id = newId, X = 0, Y = 0, Rotation = 0 };
            var conn = new ClientConnection(newId, sender, state, nowMs);

            connections[newId] = conn;
            endpointToId[sender] = newId;

            wasExisting = false;
            return newId;
        }

        public void TouchLastHeard(IPEndPoint sender, long nowMs)
        {
            if (endpointToId.TryGetValue(sender, out var id) &&
                connections.TryGetValue(id, out var c))
            {
                c.LastHeardMs = nowMs;
            }
        }

        public bool RemoveIfMatches(IPEndPoint sender, Guid claimedId, out Guid removedId)
        {
            removedId = Guid.Empty;

            if (!endpointToId.TryGetValue(sender, out var endpointId))
                return false;

            if (claimedId != endpointId)
                return false;

            Remove(endpointId);
            removedId = endpointId;
            return true;
        }

        public bool Remove(Guid id)
        {
            if (!connections.TryGetValue(id, out var c))
                return false;

            endpointToId.Remove(c.EndPoint);
            connections.Remove(id);
            return true;
        }

        public List<Guid> GetTimedOutPlayers(long nowMs, int timeoutMs)
        {
            var toRemove = new List<Guid>();
            foreach (var kvp in connections)
            {
                var id = kvp.Key;
                var c = kvp.Value;
                if (nowMs - c.LastHeardMs >= timeoutMs)
                    toRemove.Add(id);
            }
            return toRemove;
        }
    }
}