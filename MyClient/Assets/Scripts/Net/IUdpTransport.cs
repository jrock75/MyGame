using System;
using System.Net;
using System.Threading;

namespace MyGame.MyClient
{
    public interface IUdpTransport : IDisposable
    {
        void BindAny();
        void Send(byte[] data, IPEndPoint endpoint);
        void StartReceiveLoop(Action<byte[]> onPacket, CancellationToken ct);
    }
}