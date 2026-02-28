namespace MyGame.Shared
{
    public enum ClientPacketType : byte
    {
        Hello = 1,
        Ping = 2,
        Input = 3,
        Disconnect = 4
    }

    public enum ServerPacketType : byte
    {
        Welcome = 1,
        Snapshot = 2,
        PlayerLeft = 3
    }
}