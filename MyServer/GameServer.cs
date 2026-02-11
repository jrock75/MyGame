using MyGame.Shared;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;


namespace MyGame.Server
{
    public class GameServer
    {
        // TCP Authentication
        private readonly TcpListener tcpListener;
        private readonly ConcurrentDictionary<Guid, string> sessions = new();

        // UDP Game
        private readonly UdpClient udp;
        private readonly IPEndPoint udpEP;
        private readonly ConcurrentDictionary<Guid, PlayerState> players = new();
        private readonly ConcurrentDictionary<Guid, IPEndPoint> endpoints = new();
        private readonly ConcurrentDictionary<Guid, DateTime> lastSeen = new();
        private readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

        private readonly float tickRate = 30f;
        private int snapshotId = 0;

        private CancellationTokenSource cts;

        public GameServer(int tcpPort, int udpPort)
        {
            tcpListener = new TcpListener(IPAddress.Any, tcpPort);
            udpEP = new IPEndPoint(IPAddress.Any, udpPort);
            udp = new UdpClient(udpEP);
        }

        #region TCP Auth
        public void StartTcp()
        {
            tcpListener.Start();
            Console.WriteLine($"TCP Auth listening on {tcpListener.LocalEndpoint}");
            Task.Run(AcceptTcpLoop);
        }

        private async Task AcceptTcpLoop()
        {
            while (true)
            {
                TcpClient client = await tcpListener.AcceptTcpClientAsync();
                _ = HandleTcpClient(client);
            }
        }

        private async Task HandleTcpClient(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int len = await stream.ReadAsync(buffer, 0, buffer.Length);
                string request = Encoding.UTF8.GetString(buffer, 0, len).Trim();
                string[] parts = request.Split(':');

                if (parts.Length != 2)
                {
                    await SendTcpError(stream, "Invalid format");
                    return;
                }

                string username = parts[0];
                string password = parts[1];

                // TODO: implement real auth
                bool valid = true;

                if (!valid)
                {
                    await SendTcpError(stream, "Invalid credentials");
                    return;
                }

                // Assign session GUID
                Guid sessionGuid = Guid.NewGuid();
                sessions[sessionGuid] = username;

                byte[] guidBytes = sessionGuid.ToByteArray();
                await stream.WriteAsync(guidBytes, 0, guidBytes.Length);
                Console.WriteLine($"Authenticated '{username}', GUID: {sessionGuid}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TCP error: {ex}");
            }
            finally
            {
                client.Close();
            }
        }

        private async Task SendTcpError(NetworkStream stream, string message)
        {
            byte[] msg = Encoding.UTF8.GetBytes("ERROR:" + message);
            await stream.WriteAsync(msg, 0, msg.Length);
        }
        #endregion

        #region UDP Game
        public void StartUdp()
        {
            Console.WriteLine($"UDP Game listening on {udpEP}");
            cts = new CancellationTokenSource();
            Task.Run(() => ReceiveLoop(cts.Token));
            Task.Run(() => TickLoop(cts.Token));
        }

        public void StopUdp()
        {
            cts.Cancel();
            udp.Close();
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try { result = await udp.ReceiveAsync(); }
                catch (ObjectDisposedException) { break; }
                catch (SocketException ex) { Console.WriteLine($"UDP receive error: {ex.SocketErrorCode}"); continue; }

                try
                {
                    using var reader = new PacketReader(result.Buffer);

                    // Read player GUID
                    Guid playerGuid = reader.ReadGuid();

                    // Validate session
                    if (!sessions.ContainsKey(playerGuid))
                    {
                        Console.WriteLine($"Unknown GUID {playerGuid}, ignoring packet");
                        continue;
                    }

                    endpoints[playerGuid] = result.RemoteEndPoint;
                    lastSeen[playerGuid] = DateTime.UtcNow;

                    PlayerState state = reader.ReadPlayerState();
                    state.PlayerGuid = playerGuid;
                    players[playerGuid] = state;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Packet read error: {ex}");
                }
            }
        }

        private async Task TickLoop(CancellationToken token)
        {
            float interval = 1f / tickRate;

            while (!token.IsCancellationRequested)
            {
                SendSnapshots();
                RemoveTimedOutPlayers();
                snapshotId++;
                await Task.Delay(TimeSpan.FromSeconds(interval), token).ContinueWith(_ => { });
            }
        }

        private void SendSnapshots()
        {
            foreach (var kvp in endpoints)
            {
                Guid guid = kvp.Key;
                IPEndPoint ep = kvp.Value;

                using var writer = new PacketWriter();
                writer.WriteGuid(guid);
                writer.WriteInt32(snapshotId);

                foreach (var p in players.Values)
                {
                    writer.WritePlayerState(p);
                }

                try { udp.Send(writer.ToArray(), writer.Length, ep); }
                catch (Exception ex) { Console.WriteLine($"Send error to {ep}: {ex.Message}"); }
            }
        }

        private void RemoveTimedOutPlayers()
        {
            DateTime now = DateTime.UtcNow;
            List<Guid> toRemove = new();

            foreach (var kvp in lastSeen)
                if (now - kvp.Value > timeout)
                    toRemove.Add(kvp.Key);

            foreach (var guid in toRemove)
            {
                players.TryRemove(guid, out _);
                endpoints.TryRemove(guid, out _);
                lastSeen.TryRemove(guid, out _);
                Console.WriteLine($"Player {guid} disconnected (timeout)");
            }
        }
        #endregion
    }
}
