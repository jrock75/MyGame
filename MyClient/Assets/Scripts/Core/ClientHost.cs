using MyGame.Shared;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using UnityEngine;

namespace MyGame.MyClient
{
    public sealed class ClientHost
    {
        private readonly IUdpTransport transport;
        private readonly PacketDispatcher dispatcher;
        private readonly ConnectionService connection;
        private readonly SnapshotService snapshots;
        private readonly InputService input;
        private readonly IInputSource inputSource;

        private readonly ConcurrentQueue<byte[]> incomingPackets = new ConcurrentQueue<byte[]>();
        private int incomingCount;

        private readonly int maxQueuedPackets = 200;
        private readonly int drainPerOverflow = 20;

        private CancellationTokenSource cts;

        private float pingTimer;
        private const float pingInterval = 0.5f;

        private float inputTimer;
        private const float inputInterval = 1f / 20f;

        // --- Server liveness / timeout detection ---
        private float serverTimeoutTimer;
        private const float serverTimeoutSeconds = 2.5f;

        public ClientHost(
            IUdpTransport transport,
            IInputSource input,
            WorldService world,
            IPEndPoint serverEP)
        {
            this.transport = transport;

            connection = new ConnectionService(transport, serverEP);
            snapshots = new SnapshotService(world);
            this.input = new InputService(transport, input, serverEP, connection);
            this.inputSource = input;

            dispatcher = new PacketDispatcher();
            dispatcher.Register(ServerPacketType.Welcome, connection.HandleWelcome);
            dispatcher.Register(ServerPacketType.Snapshot, snapshots.HandleSnapshot);
            dispatcher.Register(ServerPacketType.PlayerLeft, snapshots.HandlePlayerLeft); // reuse world service pipeline
        }

        public void Start()
        {
            cts = new CancellationTokenSource();

            // Bind to any free local port (same behavior as before)
            transport.BindAny();

            // Start receive loop in background thread/task
            transport.StartReceiveLoop(OnPacketReceived, cts.Token);

            // Immediately send Hello
            connection.SendHello();
        }

        public void Tick(float dt)
        {
            // MAIN THREAD drain
            while (incomingPackets.TryDequeue(out var data))
            {
                Interlocked.Decrement(ref incomingCount);
                dispatcher.Dispatch(data);
            }

            // Detect server timeout (UDP has no "disconnect" event)
            if (connection.IsConnected)
            {
                serverTimeoutTimer += dt;
                if (serverTimeoutTimer >= serverTimeoutSeconds)
                {
                    // Guard: if another code path already marked disconnected
                    if (!connection.IsConnected) return;

                    Debug.LogWarning("Server timed out (no packets received).");
                    connection.MarkDisconnected();
                    snapshots.Cleanup();
                    return;
                }
            }

            if (!connection.IsConnected)
                return;

            pingTimer += dt;
            if (pingTimer >= pingInterval)
            {
                pingTimer = 0f;
                connection.SendPing();
            }

            inputTimer += dt;
            if (inputTimer >= inputInterval)
            {
                float sendDt = inputTimer;
                inputTimer = 0f;
                this.input.SendInput(sendDt);
            }
        }

        public void SendDisconnect() => connection.SendDisconnectBestEffort();

        public void Stop()
        {
            // best-effort disconnect first
            SendDisconnect();

            if (inputSource is IDisposable d)
                d.Dispose();

            cts?.Cancel();
            cts?.Dispose();
            cts = null;

            // despawn everything on main thread (we are in OnDestroy typically)
            snapshots.Cleanup();

            transport.Dispose();
        }

        private void MarkServerHeard()
        {
            serverTimeoutTimer = 0f;
        }

        private void OnPacketReceived(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return;

            // Any server packet counts as "server is alive"
            MarkServerHeard();

            incomingPackets.Enqueue(buffer);
            int count = Interlocked.Increment(ref incomingCount);

            if (count > maxQueuedPackets)
            {
                int dropped = 0;

                while (dropped < drainPerOverflow && incomingPackets.TryDequeue(out _))
                {
                    Interlocked.Decrement(ref incomingCount);
                    dropped++;
                }

                if (Time.frameCount % 60 == 0)
                    Debug.LogWarning($"Packet backlog overflow. Dropped {dropped} old packets. Queue={incomingCount}");
            }
        }
    }
}