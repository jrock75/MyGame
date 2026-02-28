using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MyGame.MyServer.Net
{
    public interface IUdpTransport
    {
        int LocalPort { get; }
        Task<(byte[] Buffer, IPEndPoint RemoteEndPoint)> ReceiveAsync(CancellationToken ct);
        void Send(byte[] data, IPEndPoint endpoint);
        void Close();
    }
}