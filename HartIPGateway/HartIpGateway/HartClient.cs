using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Communication.HartLite;

namespace HartIPGateway.HartIpGateway
{
    public class HartClient
    {
        private readonly HartIpGatewayServer _hartTcpGateway;

        public HartClient(HartIpGatewayServer HartTcpGateway, TcpClient HartTcpClient)
        {
            _hartTcpGateway = HartTcpGateway;
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
                _hartTcpGateway.AddHartClientToGateway(this);

                NetworkStream networkStream = _tcpHartClient.GetStream();

                var isClientConnected = true;

                while (isClientConnected)
                {


                    var requestHeaderBytes = ReceiveMessageFromStream(networkStream, HartConstants.HART_MSG_HEADER_SIZE);

                    if (requestHeaderBytes.Count < HartConstants.HART_MSG_HEADER_SIZE)
                    {

                        isClientConnected = _tcpHartClient.Connected;

                        if (!isClientConnected)
                        {
                            break;
                        }

                        continue;

                    }

                    var requestHeader = new HartMessageHeader(requestHeaderBytes.ToArray());
                    var requestDataBytes = new List<byte>();

                    if (requestHeader.ByteCount > requestHeaderBytes.Count())
                    {
                        var bodySize = requestHeader.ByteCount - requestHeaderBytes.Count();
                        requestDataBytes = ReceiveMessageFromStream(networkStream, bodySize);
                    }

                    var fullRequest = new List<byte>(requestHeader.HeaderBytes);
                    fullRequest.AddRange(requestDataBytes);

                    Console.Write("Request:");

                    for (int i = 0; i < fullRequest.Count(); i++)
                    {
                        Console.Write($"{fullRequest[i]} ");
                    }

                    Console.WriteLine(" ");

                    switch (requestHeader.MessageId)
                    {
                        case MsgIdType.SessionInitiate:
                            HandleSessionInitiate(networkStream, requestHeader, requestDataBytes);
                            break;
                        case MsgIdType.SessionClose:
                            HandleSessionClose(networkStream, requestHeader, requestDataBytes);
                            break;
                        case MsgIdType.KeepAlive:
                            HandleKeepAlive(networkStream, requestHeader, requestDataBytes);
                            break;
                        case MsgIdType.TokenPassingPDU:
                            HandleTokenPassingPDU(networkStream, requestHeader, requestDataBytes);
                            break;
                        default:
                            Console.WriteLine(" Not Implemented MessageId:", requestHeader.MessageId);
                            break;
                    }

                    Thread.Sleep(1);

                    isClientConnected = _tcpHartClient.Connected;
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine("Error  Handling Client Request:" + ex.Message);
            }
            finally
            {
                _hartTcpGateway.RemoveHartClientFromGateway(this);
                _tcpHartClient.Close();
            }

        }

        private void HandleTokenPassingPDU(NetworkStream networkStream, HartMessageHeader requestHeader, IList<byte> requestDataBytes)
        {

            var rawResult = _hartTcpGateway.HartSerial.SendRawCommand(requestDataBytes.ToArray());
            
            if (rawResult == null)
            {
                return;
            }

            byte statusCode = 0;

            var hartIpHeaderResponse = new HartMessageHeader(requestHeader.Version, MsgType.Response, MsgIdType.TokenPassingPDU, statusCode, requestHeader.SequenceNumber, (ushort)(HARTIPMessage.HART_MSG_HEADER_SIZE + rawResult.Length));
            var responseBody = rawResult;
            var response = new List<byte>();
            response.AddRange(hartIpHeaderResponse.HeaderBytes);
            response.AddRange(responseBody);

            Console.Write("Reponse HandleTokenPassingPDU:");
            SendResponse(networkStream, response);
        }

        private uint _inactivityCloseTimeMiliSeconds;

        private void HandleSessionInitiate(NetworkStream networkStream, HartMessageHeader requestHeader, IList<byte> requestDataBytes)
        {
            var hartIpHeaderResponse = new HartMessageHeader(requestHeader.Version, MsgType.Response, MsgIdType.SessionInitiate, 0, requestHeader.SequenceNumber, HARTIPMessage.HART_MSG_HEADER_SIZE + 5);

            _inactivityCloseTimeMiliSeconds = ByteConverterUtil.ToUint32(requestDataBytes[4], requestDataBytes[3], requestDataBytes[2], requestDataBytes[1]);

            var responseBody = new byte[5];
            responseBody[0] = 1;
            responseBody[1] = requestDataBytes[1];
            responseBody[2] = requestDataBytes[2];
            responseBody[3] = requestDataBytes[3];
            responseBody[4] = requestDataBytes[4];
            var response = new List<byte>();
            response.AddRange(hartIpHeaderResponse.HeaderBytes);
            response.AddRange(responseBody);

            Console.Write("Reponse HandleSessionInitiate:");
            SendResponse(networkStream, response);
        }


        private void HandleSessionClose(NetworkStream networkStream, HartMessageHeader requestHeader, IList<byte> requestDataBytes)
        {
            var hartIpHeaderResponse = new HartMessageHeader(requestHeader.Version, MsgType.Response, MsgIdType.SessionClose, 0, requestHeader.SequenceNumber, HARTIPMessage.HART_MSG_HEADER_SIZE + 0);

            var responseBody = new byte[0];
            var response = new List<byte>();
            response.AddRange(hartIpHeaderResponse.HeaderBytes);
            response.AddRange(responseBody);

            Console.Write("Reponse HandleSessionClose:");
            SendResponse(networkStream, response);
            _tcpHartClient.Close();
        }

        private void HandleKeepAlive(NetworkStream networkStream, HartMessageHeader requestHeader, IList<byte> requestDataBytes)
        {
            var hartIpHeaderResponse = new HartMessageHeader(requestHeader.Version, MsgType.Response, MsgIdType.KeepAlive, 0, requestHeader.SequenceNumber, HARTIPMessage.HART_MSG_HEADER_SIZE + 0);

            var responseBody = new byte[0];
            var response = new List<byte>();
            response.AddRange(hartIpHeaderResponse.HeaderBytes);
            response.AddRange(responseBody);

            Console.Write("Reponse KeepAlive:");
            SendResponse(networkStream, response);
        }


        private void SendResponse(NetworkStream networkStream, List<byte> response)
        {
            for (int i = 0; i < response.Count(); i++)
            {
                Console.Write($"{response[i]} ");
            }

            Console.WriteLine(" ");

            networkStream.Write(response.ToArray(), 0, response.Count());
        }


        private List<byte> ReceiveMessageFromStream(NetworkStream networkStream, int size)
        {
            var receivedBytes = new List<byte>();
            byte[] myReadBuffer = new byte[1024];
            int numberOfBytesRead = 0;

            if (networkStream.CanRead)
            {

                do
                {
                    var bytesToRead = size - receivedBytes.Count();
                    bytesToRead = Math.Min(myReadBuffer.Length, bytesToRead);
                    networkStream.ReadTimeout = 3000000;
                    numberOfBytesRead = networkStream.Read(myReadBuffer, 0, bytesToRead);

                    if (numberOfBytesRead > 0)
                    {
                        var bytesRead = myReadBuffer.Take(numberOfBytesRead).ToArray();
                        receivedBytes.AddRange(bytesRead);
                    }

                    if (receivedBytes.Count() >= size)
                    {
                        break;
                    }

                    Thread.Sleep(10);

                }
                while (networkStream.DataAvailable);

            }

            return receivedBytes;
        }


    }


}
