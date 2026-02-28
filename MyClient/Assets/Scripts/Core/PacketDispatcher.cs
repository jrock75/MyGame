using MyGame.Shared;
using System;
using System.Collections.Generic;

namespace MyGame.MyClient
{
    public sealed class PacketDispatcher
    {
        private readonly Dictionary<ServerPacketType, Action<byte[]>> handlers = new();

        public void Register(ServerPacketType type, Action<byte[]> handler)
        {
            handlers[type] = handler;
        }

        public void Dispatch(byte[] data)
        {
            if (data == null || data.Length < 1) return;

            var type = (ServerPacketType)data[0];
            if (handlers.TryGetValue(type, out var handler))
                handler(data);
        }
    }
}