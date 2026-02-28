using MyGame.Shared;
using System;
using System.Net;

namespace MyGame.MyClient
{
    public sealed class ConnectionService
    {
        private readonly IUdpTransport transport;
        private readonly IPEndPoint serverEP;

        private bool disconnectSent;

        public Guid MyId { get; private set; } = Guid.Empty;
        public bool IsConnected { get; private set; }

        public ConnectionService(IUdpTransport transport, IPEndPoint serverEP)
        {
            this.transport = transport;
            this.serverEP = serverEP;
        }

        public void SendHello()
        {
            transport.Send(new byte[] { (byte)ClientPacketType.Hello }, serverEP);
        }

        public void SendPing()
        {
            transport.Send(new byte[] { (byte)ClientPacketType.Ping }, serverEP);
        }

        public void HandleWelcome(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 17) return;

            byte[] guidBytes = new byte[16];
            Buffer.BlockCopy(bytes, 1, guidBytes, 0, 16);

            MyId = new Guid(guidBytes);
            IsConnected = true;
            disconnectSent = false;

            UnityEngine.Debug.Log($"Connected! My ID: {MyId}");
        }

        public void MarkDisconnected()
        {
            IsConnected = false;
            MyId = Guid.Empty;

            UnityEngine.Debug.LogWarning("Disconnected from server.");
        }

        public void SendDisconnectBestEffort()
        {
            if (disconnectSent) return;
            disconnectSent = true;

            if (!IsConnected || MyId == Guid.Empty)
                return;

            byte[] data = new byte[1 + 16];
            data[0] = (byte)ClientPacketType.Disconnect;

            byte[] guidBytes = MyId.ToByteArray();
            Buffer.BlockCopy(guidBytes, 0, data, 1, 16);

            // Best-effort double send
            transport.Send(data, serverEP);
            transport.Send(data, serverEP);
        }
    }
}