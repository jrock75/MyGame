using MyGame.MyServer.Core;
using MyGame.MyServer.Net;
using MyGame.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MyGame.MyServer
{
    public sealed class ServerHost
    {
        private readonly IUdpTransport transport;
        private readonly PlayerRegistry players = new();
        private readonly SnapshotService snapshots = new();
        private readonly PacketDispatcher dispatcher = new();

        private readonly object sync = new();

        private CancellationTokenSource? cts;
        private Task? receiveTask;
        private Task? tickTask;

        // constants (easy tuning)
        private const int Port = 7777;
        private const int TickRate = 20;
        private const int TimeoutMs = 5000;
        private const float Speed = 5f;
        private const float MaxDt = 0.1f;

        // shared PlayerLeft buffer template (we still clone for safety)
        private readonly byte[] playerLeftBuf = new byte[17];

        public ServerHost() : this(new UdpTransport(Port)) { }

        public ServerHost(IUdpTransport transport)
        {
            this.transport = transport;

            // Wire handlers
            dispatcher.Register(ClientPacketType.Hello, (b, ep) => HandleHello(ep));
            dispatcher.Register(ClientPacketType.Ping, (b, ep) => HandlePing(ep));
            dispatcher.Register(ClientPacketType.Input, (b, ep) => HandleInput(b, ep));
            dispatcher.Register(ClientPacketType.Disconnect, (b, ep) => HandleDisconnect(b, ep));
        }

        public void Start()
        {
            if (cts != null) return;

            Console.WriteLine($"Server running on port {transport.LocalPort}...");

            cts = new CancellationTokenSource();

            receiveTask = Task.Run(() => ReceiveLoop(cts.Token));
            tickTask = Task.Run(() => TickLoop(cts.Token));
        }

        public void Stop()
        {
            if (cts == null) return;

            try
            {
                cts.Cancel();
            }
            catch { }

            // Closing transport breaks ReceiveAsync immediately
            transport.Close();

            try
            {
                var tasks = new List<Task>(2);
                if (receiveTask != null) tasks.Add(receiveTask);
                if (tickTask != null) tasks.Add(tickTask);

                if (tasks.Count > 0)
                    Task.WaitAll(tasks.ToArray(), millisecondsTimeout: 2000);
            }
            catch
            {
                // ignore shutdown timing issues
            }

            cts.Dispose();
            cts = null;

            receiveTask = null;
            tickTask = null;

            Console.WriteLine("Server stopped.");
        }

        private async Task ReceiveLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var (buffer, remote) = await transport.ReceiveAsync(ct);
                    dispatcher.Dispatch(buffer, remote);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (IOException)
                {
                    // can happen during socket close
                    if (ct.IsCancellationRequested) break;
                }
                catch (Exception ex)
                {
                    // keep running
                    Console.WriteLine(ex);
                }
            }
        }

        private void TickLoop(CancellationToken ct)
        {
            int tickDelay = 1000 / TickRate;
            int next = Environment.TickCount + tickDelay;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    int now = Environment.TickCount;
                    int sleep = next - now;
                    if (sleep > 0) Thread.Sleep(sleep);

                    next += tickDelay;

                    RemoveTimedOutPlayers();
                    BroadcastSnapshot();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"TickLoop error: {ex}");
                }
            }
        }

        // -------- Handlers --------

        private void HandleHello(IPEndPoint sender)
        {
            long now = Environment.TickCount64;
            Guid idToWelcome;
            bool existing;

            lock (sync)
            {
                idToWelcome = players.GetOrCreatePlayer(sender, now, out existing);
            }

            if (!existing)
                Console.WriteLine($"Player connected: {idToWelcome}");

            SendWelcome(idToWelcome, sender);
        }

        private void SendWelcome(Guid id, IPEndPoint sender)
        {
            byte[] data = new byte[1 + 16];
            data[0] = (byte)ServerPacketType.Welcome;
            Buffer.BlockCopy(id.ToByteArray(), 0, data, 1, 16);

            transport.Send(data, sender);
            Console.WriteLine($"Sent Welcome to {sender} with id {id}");
        }

        private void HandlePing(IPEndPoint sender)
        {
            long now = Environment.TickCount64;
            lock (sync)
            {
                players.TouchLastHeard(sender, now);
            }
        }

        private void HandleDisconnect(byte[] bytes, IPEndPoint sender)
        {
            if (bytes == null || bytes.Length < 17) return;

            byte[] guidBytes = new byte[16];
            Buffer.BlockCopy(bytes, 1, guidBytes, 0, 16);
            var claimedId = new Guid(guidBytes);

            byte[] packet;
            IPEndPoint[] endpoints;
            Guid removedId;

            lock (sync)
            {
                if (!players.RemoveIfMatches(sender, claimedId, out removedId))
                    return;

                BuildPlayerLeftPacketAndEndpointsLocked(removedId, out packet, out endpoints);
            }

            foreach (var ep in endpoints)
                transport.Send(packet, ep);

            Console.WriteLine($"Player disconnected (graceful): {removedId}");
        }

        private void HandleInput(byte[] bytes, IPEndPoint sender)
        {
            if (bytes == null || bytes.Length < 29) return;

            Guid id;
            float inputX, inputY, dt;

            try
            {
                using var ms = new MemoryStream(bytes, 1, bytes.Length - 1);
                using var br = new BinaryReader(ms);

                id = new Guid(br.ReadBytes(16));
                inputX = br.ReadSingle();
                inputY = br.ReadSingle();
                dt = br.ReadSingle();
            }
            catch
            {
                return;
            }

            // clamp input
            inputX = Math.Clamp(inputX, -1f, 1f);
            inputY = Math.Clamp(inputY, -1f, 1f);
            dt = Math.Clamp(dt, 0f, MaxDt);

            long now = Environment.TickCount64;

            lock (sync)
            {
                if (!players.TryGetIdByEndpoint(sender, out var endpointId))
                    return;

                if (id != endpointId)
                    return;

                players.TouchLastHeard(sender, now);

                if (!players.TryGetConnection(id, out var c) || c == null)
                    return;

                var p = c.State;
                p.X += inputX * Speed * dt;
                p.Y += inputY * Speed * dt;
            }
        }

        // -------- Tick work --------

        private void RemoveTimedOutPlayers()
        {
            long now = Environment.TickCount64;

            List<Guid> toRemove;
            var toSend = new List<(byte[] packet, IPEndPoint[] endpoints)>();

            lock (sync)
            {
                toRemove = players.GetTimedOutPlayers(now, TimeoutMs);

                foreach (var id in toRemove)
                {
                    if (!players.Remove(id))
                        continue;

                    BuildPlayerLeftPacketAndEndpointsLocked(id, out var packet, out var endpoints);
                    toSend.Add((packet, endpoints));

                    Console.WriteLine($"Player timed out/removed: {id}");
                }
            }

            // send outside lock
            foreach (var item in toSend)
            {
                foreach (var ep in item.endpoints)
                    transport.Send(item.packet, ep);
            }
        }

        private void BroadcastSnapshot()
        {
            byte[] snapshotBytes;
            IPEndPoint[] endpoints;

            long now = Environment.TickCount64;

            lock (sync)
            {
                if (players.Count == 0)
                    return;

                snapshotBytes = snapshots.BuildSnapshotPacket(players, now);

                endpoints = new IPEndPoint[players.Count];
                int i = 0;
                foreach (var c in players.Connections)
                    endpoints[i++] = c.EndPoint;
            }

            foreach (var ep in endpoints)
                transport.Send(snapshotBytes, ep);
        }

        // -------- PlayerLeft packet helper --------

        private void BuildPlayerLeftPacketAndEndpointsLocked(Guid id, out byte[] packet, out IPEndPoint[] endpoints)
        {
            playerLeftBuf[0] = (byte)ServerPacketType.PlayerLeft;

            Span<byte> guid = stackalloc byte[16];
            id.TryWriteBytes(guid);
            guid.CopyTo(playerLeftBuf.AsSpan(1, 16));

            // clone so our shared buffer isn't mutated mid-send
            packet = (byte[])playerLeftBuf.Clone();

            endpoints = new IPEndPoint[players.Count];
            int i = 0;
            foreach (var c in players.Connections)
                endpoints[i++] = c.EndPoint;
        }
    }
}