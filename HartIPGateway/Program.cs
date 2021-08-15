using System.Threading;
using HartIPGateway.HartIpGateway;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HartIPGateway
{
    class Program
    {
        static AutoResetEvent closeAppicationEvent = new AutoResetEvent(false);

        static void Main(string[] args)
        {
            var hartIpGatewayServer = new HartIpGatewayServer("localhost",5094,"COM2");
            hartIpGatewayServer.Start(); 

            Console.WriteLine("Waiting SignalTo Close Application");
            closeAppicationEvent.WaitOne();
            hartIpGatewayServer.Stop(); 

        }

        static void CloseApplication()
        {
            closeAppicationEvent.Set();
        }

    }
}
