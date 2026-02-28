using MyGame.Shared;
using System;
using System.Collections.Generic;
using System.IO;

namespace MyGame.MyClient
{
    public sealed class SnapshotService
    {
        private const bool DebugSnapshots = false;

        private readonly WorldService world;

        private int lastSnapshotId;
        private long lastServerTimeMs;

        public SnapshotService(WorldService world)
        {
            this.world = world;
        }

        public void HandlePlayerLeft(byte[] bytes)
        {
            // 1(type) + 16(guid)
            if (bytes == null || bytes.Length < 17) return;

            try
            {
                byte[] guidBytes = new byte[16];
                Buffer.BlockCopy(bytes, 1, guidBytes, 0, 16);
                var id = new Guid(guidBytes);

                world.RemovePlayer(id);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"Failed to parse PlayerLeft: {e.Message}");
            }
        }

        public void HandleSnapshot(byte[] bytes)
        {
            // 1 (type) + 4 (snapshotId) + 8 (serverTimeMs) + 4 (count) = 17
            if (bytes == null || bytes.Length < 17)
                return;

            try
            {
                using var ms = new MemoryStream(bytes, 1, bytes.Length - 1);
                using var br = new BinaryReader(ms);

                int snapshotId = br.ReadInt32();
                long serverTimeMs = br.ReadInt64();
                int count = br.ReadInt32();

                if (snapshotId <= lastSnapshotId)
                    return;

                lastSnapshotId = snapshotId;
                lastServerTimeMs = serverTimeMs;

                var seen = new HashSet<Guid>();

                for (int i = 0; i < count; i++)
                {
                    var id = new Guid(br.ReadBytes(16));
                    float x = br.ReadSingle();
                    float y = br.ReadSingle();
                    float rot = br.ReadSingle();

                    seen.Add(id);
                    world.UpsertPlayer(id, x, y, rot);
                }

                world.DespawnNotIn(seen);

                if (DebugSnapshots && lastSnapshotId % 100 == 0)
                    UnityEngine.Debug.Log($"Snapshot {lastSnapshotId} serverTimeMs={lastServerTimeMs}");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"Failed to parse snapshot: {e.Message}");
            }
        }

        public void Cleanup() => world.CleanupAll();
    }
}