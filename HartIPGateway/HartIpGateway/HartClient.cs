using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace HartIPGateway.HartIpGateway
{
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

            var isClientConnected = true;

            var socket = _tcpHartClient.Client;
            var socketHandle = socket.Handle.ToInt32();


            try
            {
                _HartTcpGateway.AddHartClientToGateway(this);


                while (isClientConnected)
                {
                    NetworkStream networkStream = _tcpHartClient.GetStream();

                    byte[] hartIpRequest = new byte[_tcpHartClient.ReceiveBufferSize + 1];

                    networkStream.Read(hartIpRequest, 0, System.Convert.ToInt32(_tcpHartClient.ReceiveBufferSize));

                    //send menssage to hart serial 

                    //Reply message to remote client 

                    //string responseString = "Conectado ao servidor.";
                    //Byte[] sendBytes = Encoding.ASCII.GetBytes(responseString);

                    // networkStream.Write(sendBytes, 0, sendBytes.Length);

                    isClientConnected = _tcpHartClient.Connected;

                }

            }
            finally
            {
                _HartTcpGateway.RemoveHartClientFromGateway(this);
                _tcpHartClient.Close();
            }

        }


        private bool started = false;

        /// <summary>
        /// This method runs on its own thread, and is responsible for
        /// receiving data from the server and raising an event when data
        /// is received
        /// </summary>
        private void ListenForPackets()
        {
            int bytesRead;

            while (started)
            {
                bytesRead = 0;

                try
                {
                    //Blocks until a message is received from the server
                    bytesRead = clientStream.Read(buffer.ReadBuffer, 0, readBufferSize);
                }
                catch
                {
                    //A socket error has occurred
                    Console.WriteLine("A socket error has occurred with the client socket " + tcpClient.ToString());
                    break;
                }

                if (bytesRead == 0)
                {
                    //The server has disconnected
                    break;
                }

                if (OnDataReceived != null)
                {
                    //Send off the data for other classes to handle
                    OnDataReceived(buffer.ReadBuffer, bytesRead);
                }

                Thread.Sleep(15);
            }

            started = false;
            Disconnect();
        }

    }


}
