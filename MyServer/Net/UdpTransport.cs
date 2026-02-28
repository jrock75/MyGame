using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MyGame.MyServer.Net
{
    public sealed class UdpTransport : IUdpTransport
    {
        private readonly UdpClient udp;

        public int LocalPort { get; }

        public UdpTransport(int port)
        {
            udp = new UdpClient(port);
            LocalPort = port;

            TryDisableUdpConnReset(udp);
        }

        private static void TryDisableUdpConnReset(UdpClient udp)
        {
            try
            {
                const int SIO_UDP_CONNRESET = -1744830452;
                udp.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0 }, null);
            }
            catch
            {
                // ignore if unsupported
            }
        }

        public async Task<(byte[] Buffer, IPEndPoint RemoteEndPoint)> ReceiveAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var result = await udp.ReceiveAsync(ct).ConfigureAwait(false);

            return (result.Buffer, result.RemoteEndPoint);
        }

        public void Send(byte[] data, IPEndPoint endpoint)
        {
            udp.Send(data, data.Length, endpoint);
        }

        public void Close()
        {
            udp.Close();
        }
    }
}