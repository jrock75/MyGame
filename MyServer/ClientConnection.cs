using MyGame.Shared;
using System;
using System.Net;

namespace MyGame.MyServer
{
    public sealed class ClientConnection
    {
        public Guid Id;
        public IPEndPoint EndPoint;
        public PlayerState State;
        public long LastHeardMs;

        public ClientConnection(Guid id, IPEndPoint endPoint, PlayerState state, long lastHeardMs)
        {
            Id = id;
            EndPoint = endPoint;
            State = state;
            LastHeardMs = lastHeardMs;
        }
    }
}
