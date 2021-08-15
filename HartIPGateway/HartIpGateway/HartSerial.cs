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

            communication.Timeout = new TimeSpan(0, 0, 5);

            var openResult = communication.Open();

            if (openResult != OpenResult.Opened)
            {
                throw new InvalidOperationException("Error Opening Com Port " + openResult.ToString());
            }

        }

        public byte[] Send(byte command)
        {

            CommandResult rawResult;

            if (command == 0)
            {
                rawResult = communication.SendZeroCommand();
            }
            else
            {
                rawResult = communication.Send(command);
            }

            if (rawResult == null)
            {
                return new byte[]{};
            }
            
            var response = rawResult.CommandByteArray();
            
            return response;

        }

        public byte[] Send(byte command, byte[] data)
        {
            var rawResult = communication.Send(command, data);
            var response = rawResult.CommandByteArray();
            return response;

        }

        public byte[] SendRawCommand(byte[] hartFrameDatawithoutPreamble, int preambleLeghtSize)
        {
            lock (this.lockComm)
            {

                try
                {
                    var rawResult = communication.SendRaw(hartFrameDatawithoutPreamble, preambleLeghtSize);

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
