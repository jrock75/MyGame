using MyGame.Shared;
using System;
using System.Collections.Generic;
using System.Net;

namespace MyGame.MyServer.Core
{
    public sealed class PacketDispatcher
    {
        private readonly Dictionary<ClientPacketType, Action<byte[], IPEndPoint>> handlers = new();

        public void Register(ClientPacketType type, Action<byte[], IPEndPoint> handler)
            => handlers[type] = handler;

        public void Dispatch(byte[] buffer, IPEndPoint sender)
        {
            if (buffer == null || buffer.Length < 1) return;

            var type = (ClientPacketType)buffer[0];
            if (handlers.TryGetValue(type, out var h))
                h(buffer, sender);
        }
    }
}