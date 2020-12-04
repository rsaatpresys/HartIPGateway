using Communication.HartLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HartIPGateway.HartIpGateway
{
    public class HartSerial
    {
        HartCommunicationLite communication;
        object lockComm;

        public HartSerial(string comPort)
        {
            lockComm = new object();
            this.ComPort = comPort;
        }

        public void Open()
        {
            this.communication = new HartCommunicationLite(this.ComPort)
            {
                AutomaticZeroCommand = false
            };

            communication.Timeout = new TimeSpan(0,0,5);

            var openResult = communication.Open();

            if (openResult != OpenResult.Opened)
            {
                throw new InvalidOperationException("Error Opening Com Port " + openResult.ToString());
            }

        }

        public byte[] SendRawCommand(byte[] hartCommand)
        {
            lock (this.lockComm)
            {

                try
                {
                    var rawResult = communication.SendRaw(hartCommand);

                    return rawResult;
                }
                catch (Exception commException)
                {
                    Console.WriteLine("Error Send Hart Command", commException.Message);
                    if (ReconnectOnError)
                    {
                        this.Close();
                        this.Open();
                    }
                }
            }

            return new byte[0];

        }

        public void Close()
        {

            if (this.communication != null)
            {
                this.communication.Close();
            }

        }

        public string ComPort { get; set; }

        public bool ReconnectOnError { get; set; }
    }
}
