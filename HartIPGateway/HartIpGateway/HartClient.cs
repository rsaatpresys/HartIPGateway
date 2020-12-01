using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace HartIPGateway.HartIpGateway
{

    /// <summary>
    /// HART IP Message class
    /// </summary>
    public static class HARTIPMessage
    {
        /// <summary>
        /// HART UDP/TCP message header size
        /// </summary>
        internal const int HART_MSG_HEADER_SIZE = 8;

        /// <summary>
        ///  HART UDP/TCP version
        /// </summary>
        internal const byte HART_UDP_TCP_MSG_VERSION = 1;

    }
    public class HartClient
    {
        private readonly HartIpGatewayServer _HartTcpGateway;

        public HartClient(HartIpGatewayServer HartTcpGateway, TcpClient HartTcpClient)
        {
            _HartTcpGateway = HartTcpGateway;
            _tcpHartClient = HartTcpClient;
        }

        public int Id { get; private set; }

        private Thread _clientThread;
        private TcpClient _tcpHartClient;

        public string Address { get; private set; }

        public override string ToString()
        {
            return "HartClient Thread:" + Id + " Address:" + Address;
        }

        public void Start()
        {
            _clientThread = new Thread(HandleTcpClientRequests);
            _clientThread.IsBackground = true;
            Id = _clientThread.ManagedThreadId;
            _clientThread.Name = "HartClientThread-" + Id + "-" + Address;
            _clientThread.Start();
        }

        public void Stop()
        {
            _tcpHartClient.Close();

            _clientThread.Join(2000);

            if (_clientThread.IsAlive)
            {
                _clientThread.Abort();
            }

        }

        private void HandleTcpClientRequests()
        {

            IPEndPoint remoteIpEndPoint = _tcpHartClient.Client.RemoteEndPoint as IPEndPoint;
            Address = remoteIpEndPoint.Address.ToString() + ":" + remoteIpEndPoint.Port;

            try
            {
                _HartTcpGateway.AddHartClientToGateway(this);

                NetworkStream networkStream = _tcpHartClient.GetStream();

                var isClientConnected = true;

                while (isClientConnected)
                {
                    var requestMessage = ReceiveMessageFromStream(networkStream);

                    if (requestMessage.Count >= 8)
                    {

                        var hartIpHeaderRequest = new HartMessageHeader(requestMessage.Take(8).ToArray());

                        switch (hartIpHeaderRequest.MessageId)
                        {
                            case MsgIdType.SessionInitiate:
                                
                                var hartIpHeaderResponse = new HartMessageHeader(hartIpHeaderRequest.Version, MsgType.Response, MsgIdType.SessionInitiate, 0, hartIpHeaderRequest.SequenceNumber, HARTIPMessage.HART_MSG_HEADER_SIZE + 5);
                                var responseBody = new byte[5];
                                responseBody[0] = 1;
                                responseBody[1] = 0;
                                responseBody[2] = 0;
                                responseBody[3] = 0;
                                responseBody[4] = 0;
                                var response = new List<byte>();
                                response.AddRange(hartIpHeaderResponse.HeaderBytes);
                                response.AddRange(responseBody);
                                networkStream.Write(response.ToArray(), 0, response.Count());

                                break;

                            default:
                                break;
                        }

                        System.Threading.Thread.Sleep(1);

                        //envia resposta 



                    }



                    isClientConnected = _tcpHartClient.Connected;
                }


            }
            finally
            {
                _HartTcpGateway.RemoveHartClientFromGateway(this);
                _tcpHartClient.Close();
            }

        }

        private static List<byte> ReceiveMessageFromStream(NetworkStream networkStream)
        {
            var requestMessage = new List<byte>();
            byte[] myReadBuffer = new byte[1024];
            int numberOfBytesRead = 0;

            if (networkStream.CanRead)
            {

                do
                {
                    numberOfBytesRead = networkStream.Read(myReadBuffer, 0, myReadBuffer.Length);

                    if (numberOfBytesRead > 0)
                    {
                        var bytesRead = myReadBuffer.Take(numberOfBytesRead).ToArray();
                        requestMessage.AddRange(bytesRead);
                    }

                    Thread.Sleep(10);

                }
                while (networkStream.DataAvailable);

            }

            return requestMessage;
        }


    }


}
