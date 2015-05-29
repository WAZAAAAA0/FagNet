using System;

namespace FagNetRelay
{
    class Program
    {
        static void Main()
        {
            RelayServer.Instance.Start();
            while (true)
            {
                var input = Console.ReadLine();
                if (input == "exit")
                    break;
            }
            RelayServer.Instance.Stop();
        }
    }
}
