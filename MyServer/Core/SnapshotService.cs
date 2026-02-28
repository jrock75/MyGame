using MyGame.Shared;
using System;
using System.IO;

namespace MyGame.MyServer.Core
{
    public sealed class SnapshotService
    {
        private int nextSnapshotId = 1;

        public byte[] BuildSnapshotPacket(PlayerRegistry players, long nowMs)
        {
            int snapshotId = nextSnapshotId++;
            long serverTimeMs = nowMs;

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write((byte)ServerPacketType.Snapshot);

            // snapshot metadata
            bw.Write(snapshotId);     // int32
            bw.Write(serverTimeMs);   // int64

            bw.Write(players.Count);

            foreach (var c in players.Connections)
            {
                var p = c.State;
                bw.Write(p.Id.ToByteArray());
                bw.Write(p.X);
                bw.Write(p.Y);
                bw.Write(p.Rotation);
            }

            return ms.ToArray();
        }
    }
}