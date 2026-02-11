using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace MyGame.Shared
{
    public sealed class PacketReader : IDisposable
    {
        private const int MaxStringLength = 1024 * 1024; // 1 MB safety cap
        private const int NullPlayerId = -1;

        private readonly MemoryStream memoryStream;
        private readonly BinaryReader reader;
        private bool disposed;

        public PacketReader(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            memoryStream = new MemoryStream(data, writable: false);
            reader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
        }

        // --------------------
        // Primitive reads
        // --------------------

        public int ReadInt32()
        {
            EnsureAvailable(sizeof(int));
            return reader.ReadInt32();
        }

        public float ReadFloat()
        {
            EnsureAvailable(sizeof(float));
            return reader.ReadSingle();
        }

        public bool ReadBoolean()
        {
            EnsureAvailable(sizeof(bool));
            return reader.ReadBoolean();
        }

        // --------------------
        // String read
        // --------------------

        public string? ReadString()
        {
            EnsureAvailable(sizeof(byte));
            byte type = reader.ReadByte();

            switch (type)
            {
                case 0: // null
                    return null;

                case 1: // empty
                    return string.Empty;

                case 2: // short string (byte length)
                    {
                        EnsureAvailable(sizeof(byte));
                        int length = reader.ReadByte();

                        ValidateStringLength(length);
                        EnsureAvailable(length);

                        return Encoding.UTF8.GetString(reader.ReadBytes(length));
                    }

                case 3: // long string (int length)
                    {
                        EnsureAvailable(sizeof(int));
                        int length = reader.ReadInt32();

                        ValidateStringLength(length);
                        EnsureAvailable(length);

                        return Encoding.UTF8.GetString(reader.ReadBytes(length));
                    }

                default:
                    throw new InvalidDataException($"Unknown string type: {type}");
            }
        }

        // --------------------
        // Math types
        // --------------------

        public Vector3 ReadVector3()
        {
            return new Vector3(
                ReadFloat(),
                ReadFloat(),
                ReadFloat()
            );
        }

        public Quaternion ReadQuaternion()
        {
            return new Quaternion(
                ReadFloat(),
                ReadFloat(),
                ReadFloat(),
                ReadFloat()
            );
        }
        public Guid ReadGuid()
        {
            byte[] bytes = reader.ReadBytes(16); // GUID is 16 bytes
            return new Guid(bytes);
        }

        // --------------------
        // Game-specific types
        // --------------------

        public PlayerState ReadPlayerState()
        {
            Guid playerGuid = ReadGuid();         // matches WriteGuid
            Vector3 pos = ReadVector3();          // matches WriteVector3
            Vector3 vel = ReadVector3();
            Quaternion rot = ReadQuaternion();
            bool alive = ReadBoolean();
            string name = ReadString();           // matches WriteString

            return new PlayerState
            {
                PlayerGuid = playerGuid,
                position = pos,
                velocity = vel,
                rotation = rot,
                isAlive = alive,
                playerName = name
            };
        }

        // --------------------
        // Position helpers
        // --------------------

        public int Position => (int)memoryStream.Position;
        public int Length => (int)memoryStream.Length;

        internal void SetPosition(int position)
        {
            if (position < 0 || position > memoryStream.Length)
                throw new ArgumentOutOfRangeException(nameof(position));

            memoryStream.Position = position;
        }

        // --------------------
        // Safety helpers
        // --------------------

        private void EnsureAvailable(int byteCount)
        {
            if (memoryStream.Length - memoryStream.Position < byteCount)
                throw new EndOfStreamException("Packet ended unexpectedly");
        }

        private static void ValidateStringLength(int length)
        {
            if (length < 0 || length > MaxStringLength)
                throw new InvalidDataException($"Invalid string length: {length}");
        }

        // --------------------
        // Dispose
        // --------------------

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            reader.Dispose();
        }
    }
}
