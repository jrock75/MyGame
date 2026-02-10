using System;
using System.IO;
using System.Text;
using UnityEngine;
using MyServer.GameLogic;

namespace MyServer.Networking
{
    public sealed class PacketWriter : IDisposable
    {
        private const int MaxStringLength = 1024 * 1024; // 1 MB
        private readonly MemoryStream memoryStream;
        private readonly BinaryWriter writer;
        private bool disposed;

        public PacketWriter(int initialCapacity = 256)
        {
            memoryStream = new MemoryStream(initialCapacity);
            writer = new BinaryWriter(memoryStream, Encoding.UTF8, leaveOpen: false);
        }

        // --------------------
        // Primitive writes
        // --------------------
        public void WriteInt32(int value) => writer.Write(value);
        public void WriteFloat(float value) => writer.Write(value);
        public void WriteBoolean(bool value) => writer.Write(value);

        // --------------------
        // GUID
        // --------------------
        public void WriteGuid(Guid guid)
        {
            writer.Write(guid.ToByteArray());
        }

        // --------------------
        // Strings
        // --------------------
        public void WriteString(string? value)
        {
            if (value == null) { writer.Write((byte)0); return; }
            if (value.Length == 0) { writer.Write((byte)1); return; }

            byte[] bytes = Encoding.UTF8.GetBytes(value);

            if (bytes.Length > MaxStringLength)
                throw new InvalidDataException($"String too large: {bytes.Length} bytes");

            if (bytes.Length <= byte.MaxValue)
            {
                writer.Write((byte)2);
                writer.Write((byte)bytes.Length);
                writer.Write(bytes);
            }
            else
            {
                writer.Write((byte)3);
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
        }

        // --------------------
        // Vector / Quaternion helpers
        // --------------------
        public void WriteVector3(Vector3 v)
        {
            WriteFloat(v.x);
            WriteFloat(v.y);
            WriteFloat(v.z);
        }

        public void WriteQuaternion(Quaternion q)
        {
            WriteFloat(q.x);
            WriteFloat(q.y);
            WriteFloat(q.z);
            WriteFloat(q.w);
        }

        // --------------------
        // PlayerState
        // --------------------
        public void WritePlayerState(PlayerState state)
        {
            // GUID must be first
            WriteGuid(state.PlayerGuid);

            // Then the rest
            WriteVector3(state.position);
            WriteVector3(state.velocity);
            WriteQuaternion(state.rotation);
            WriteBoolean(state.isAlive);
            WriteString(state.playerName);
        }

        // --------------------
        // Buffer access
        // --------------------
        public ReadOnlySpan<byte> AsSpan() => memoryStream.GetBuffer().AsSpan(0, (int)memoryStream.Length);
        public byte[] ToArray() => memoryStream.ToArray();
        public int Length => (int)memoryStream.Length;

        public void Reset()
        {
            memoryStream.Position = 0;
            memoryStream.SetLength(0);
        }

        // --------------------
        // Dispose
        // --------------------
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            writer.Dispose();
        }
    }
}
