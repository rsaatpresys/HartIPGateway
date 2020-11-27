using HartIPGateway.HartIpGateway;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HartIPGateway
{
    class Program
    {
        static void Main(string[] args)
        {

            var hartIpGatewayServer = new HartIpGatewayServer("localhost",5094,"COM3");
            hartIpGatewayServer.Start(); 

            Console.WriteLine("Aperte Qualquer Tecla para finalizar"); 
            Console.ReadKey();
            hartIpGatewayServer.Stop(); 

        }
    }
}
