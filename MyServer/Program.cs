using MyServer;

class Program
{
    public static void Main()
    {
        int tcpPort = 9000;
        int udpPort = 7777;

        var server = new GameServer(tcpPort, udpPort);
        server.StartTcp();
        server.StartUdp();

        Console.WriteLine("Press ENTER to stop server...");
        Console.ReadLine();
        server.StopUdp();
    }
}