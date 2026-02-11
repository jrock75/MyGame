using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace MyGame.Shared
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

        public void WriteInt32(int value)
        {
            writer.Write(value);
        }

        public void WriteFloat(float value)
        {
            writer.Write(value);
        }

        public void WriteBoolean(bool value)
        {
            writer.Write(value);
        }

        // --------------------
        // String write (matches PacketReader exactly)
        // --------------------

        public void WriteString(string? value)
        {
            if (value == null)
            {
                writer.Write((byte)0); // null
                return;
            }

            if (value.Length == 0)
            {
                writer.Write((byte)1); // empty
                return;
            }

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(value);

            if (utf8Bytes.Length > MaxStringLength)
                throw new InvalidDataException($"String too large: {utf8Bytes.Length} bytes");

            if (utf8Bytes.Length <= byte.MaxValue)
            {
                writer.Write((byte)2); // short string
                writer.Write((byte)utf8Bytes.Length);
                writer.Write(utf8Bytes);
            }
            else
            {
                writer.Write((byte)3); // long string
                writer.Write(utf8Bytes.Length);
                writer.Write(utf8Bytes);
            }
        }

        // --------------------
        // Math types
        // --------------------

        public void WriteVector3(Vector3 vector)
        {
            WriteFloat(vector.X);
            WriteFloat(vector.Y);
            WriteFloat(vector.Z);
        }

        public void WriteQuaternion(Quaternion quaternion)
        {
            WriteFloat(quaternion.X);
            WriteFloat(quaternion.Y);
            WriteFloat(quaternion.Z);
            WriteFloat(quaternion.W);
        }

        public void WriteGuid(Guid guid)
        {
            byte[] bytes = guid.ToByteArray(); // always 16 bytes
            writer.Write(bytes);
        }

        // --------------------
        // Game-specific types
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

        public ReadOnlySpan<byte> AsSpan()
        {
            return memoryStream.GetBuffer().AsSpan(0, (int)memoryStream.Length);
        }

        public byte[] ToArray()
        {
            return memoryStream.ToArray();
        }

        public int Position => (int)memoryStream.Position;
        public int Length => (int)memoryStream.Length;

        internal void SetPosition(int position)
        {
            if (position < 0 || position > memoryStream.Length)
                throw new ArgumentOutOfRangeException(nameof(position));

            memoryStream.Position = position;
        }

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
            if (disposed)
                return;

            disposed = true;
            writer.Dispose();
        }
    }
}
