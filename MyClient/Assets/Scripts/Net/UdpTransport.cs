using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MyGame.MyClient
{
    public sealed class UdpTransport : IUdpTransport
    {
        private UdpClient udp;

        // One-time warning flag for "server down" noise on Windows UDP
        private int serverDownWarned;

        public void BindAny()
        {
            udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            TryDisableUdpConnReset(udp); // prevents spam when server dies on Windows
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

        private void WarnServerDownOnce()
        {
            if (Interlocked.Exchange(ref serverDownWarned, 1) == 0)
                Debug.LogWarning("Server appears to be down (UDP port unreachable / connection reset).");
        }

        public void Send(byte[] data, IPEndPoint endpoint)
        {
            if (udp == null) return;

            try
            {
                udp.Send(data, data.Length, endpoint);
            }
            catch (SocketException se) when (se.SocketErrorCode == SocketError.ConnectionReset)
            {
                WarnServerDownOnce();
                // ignore to avoid log spam
            }
            catch (SocketException se)
            {
                Debug.LogWarning($"Socket send failed: {se.Message}");
            }
        }

        public void StartReceiveLoop(Action<byte[]> onPacket, CancellationToken ct)
        {
            if (udp == null) throw new InvalidOperationException("BindAny must be called before StartReceiveLoop.");

            _ = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested && udp != null)
                {
                    try
                    {
                        var result = await udp.ReceiveAsync().ConfigureAwait(false);
                        onPacket?.Invoke(result.Buffer);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (SocketException se) when (se.SocketErrorCode == SocketError.ConnectionReset)
                    {
                        WarnServerDownOnce();
                        // ignore to prevent log spam
                    }
                    catch (SocketException se)
                    {
                        Debug.LogWarning($"Socket error: {se.Message}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                    }
                }
            }, ct);
        }

        public void Dispose()
        {
            udp?.Close();
            udp = null;
        }
    }
}