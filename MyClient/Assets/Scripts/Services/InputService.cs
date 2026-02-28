using MyGame.Shared;
using System.IO;
using System.Net;
using UnityEngine;

namespace MyGame.MyClient
{
    public sealed class InputService
    {
        private readonly IUdpTransport transport;
        private readonly IInputSource input;
        private readonly IPEndPoint serverEP;
        private readonly ConnectionService connection;

        public InputService(IUdpTransport transport, IInputSource input, IPEndPoint serverEP, ConnectionService connection)
        {
            this.transport = transport;
            this.input = input;
            this.serverEP = serverEP;
            this.connection = connection;
        }

        public void SendInput(float dt)
        {
            if (connection.MyId == System.Guid.Empty) return;

            Vector2 move = input != null ? input.ReadMove() : Vector2.zero;

            if (move.sqrMagnitude < 0.0001f)
                move = Vector2.zero;

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write((byte)ClientPacketType.Input);
            bw.Write(connection.MyId.ToByteArray());
            bw.Write(move.x);
            bw.Write(move.y);
            bw.Write(dt);

            var data = ms.ToArray();
            transport.Send(data, serverEP);
        }
    }
}