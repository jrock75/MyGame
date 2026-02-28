using System;

namespace MyGame.MyServer
{
    public class Program
    {
        private static void Main()
        {
            var server = new ServerHost();
            server.Start();

            Console.WriteLine("Press ENTER to stop...");
            Console.ReadLine();

            server.Stop();
        }
    }
}