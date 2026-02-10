using System;
using System.IO;
using System.Text;
using UnityEngine;
using MyServer.GameLogic;

namespace MyServer.Networking
{
    public sealed class PacketReader : IDisposable
    {
        private const int MaxStringLength = 1024 * 1024;
        private readonly MemoryStream memoryStream;
        private readonly BinaryReader reader;
        private bool disposed;

        public PacketReader(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            memoryStream = new MemoryStream(data, writable: false);
            reader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
        }

        // --------------------
        // Primitive reads
        // --------------------
        public int ReadInt32() { EnsureAvailable(sizeof(int)); return reader.ReadInt32(); }
        public float ReadFloat() { EnsureAvailable(sizeof(float)); return reader.ReadSingle(); }
        public bool ReadBoolean() { EnsureAvailable(sizeof(bool)); return reader.ReadBoolean(); }

        // --------------------
        // GUID
        // --------------------
        public Guid ReadGuid()
        {
            EnsureAvailable(16);
            byte[] bytes = reader.ReadBytes(16);
            return new Guid(bytes);
        }

        // --------------------
        // Strings
        // --------------------
        public string? ReadString()
        {
            EnsureAvailable(sizeof(byte));
            byte type = reader.ReadByte();
            switch (type)
            {
                case 0: return null;
                case 1: return string.Empty;
                case 2:
                    {
                        EnsureAvailable(sizeof(byte));
                        int len = reader.ReadByte();
                        ValidateStringLength(len);
                        EnsureAvailable(len);
                        return Encoding.UTF8.GetString(reader.ReadBytes(len));
                    }
                case 3:
                    {
                        EnsureAvailable(sizeof(int));
                        int len = reader.ReadInt32();
                        ValidateStringLength(len);
                        EnsureAvailable(len);
                        return Encoding.UTF8.GetString(reader.ReadBytes(len));
                    }
                default:
                    throw new InvalidDataException($"Unknown string type: {type}");
            }
        }

        // --------------------
        // Vector / Quaternion
        // --------------------
        public Vector3 ReadVector3()
        {
            return new Vector3(ReadFloat(), ReadFloat(), ReadFloat());
        }

        public Quaternion ReadQuaternion()
        {
            return new Quaternion(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());
        }

        // --------------------
        // PlayerState
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
        public bool HasRemaining => Position < Length;

        internal void SetPosition(int position)
        {
            if (position < 0 || position > memoryStream.Length) throw new ArgumentOutOfRangeException();
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
            if (disposed) return;
            disposed = true;
            reader.Dispose();
        }
    }
}
