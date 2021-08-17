using System.Threading;
using HartIPGateway.HartIpGateway;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HartIPGatewayCF;

namespace HartIPGateway
{
    class Program
    {
        static AutoResetEvent closeAppicationEvent = new AutoResetEvent(false);

        static void Main(string[] args)
        {
            var portCalibrator = "COM3";
            var hartModem = "COM2";
            var hartIpPort = 5094;

            
            if (args.Any(t => t.ToLower().Contains("/hart")))
            {
                var hartArg = args.First(t => t.ToLower().Contains("/hart"));

                if (hartArg.ToUpper().Contains("ENABLE_HART_INTERNAL_RESISTOR"))
                {
                    if (!AnalogUnitSolidStateRelaysControl.Open(portCalibrator))
                    {
                        Console.WriteLine("failed to open port for relaycontrol " + portCalibrator);
                    }
                    AnalogUnitSolidStateRelaysControl.EnableHartWithInternalResistor();
                    AnalogUnitSolidStateRelaysControl.Close();

                }
                else if (hartArg.ToUpper().Contains("ENABLE_HART_NO_RESISTOR"))
                {
                    if (!AnalogUnitSolidStateRelaysControl.Open(portCalibrator))
                    {
                        Console.WriteLine("failed to open port for relaycontrol " + portCalibrator);
                    }
                    AnalogUnitSolidStateRelaysControl.EnableHartWithInternalResistor();
                    AnalogUnitSolidStateRelaysControl.Close();

                }
                else
                {
                    Console.WriteLine("Invalid Hart Option. Hart Relay Not Opened"); 
                }
            }

            var hartIpGatewayServer = new HartIpGatewayServer("localhost", hartIpPort, hartModem);
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
