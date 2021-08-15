using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace HartIPGateway.HartIpGateway
{
    public class HartIpGatewayServer
    {
        private readonly string _gatewayAddress;
        private readonly int _gatewayPort;
        private readonly string _serialComPort;

        private TcpListener _gatewayListener;
        private bool _isRunning;
        private HartSerial _hartSerial;

        public HartIpGatewayServer(string gatewayAddress, int gatewayPort, string serialComPort)
        {
            _gatewayAddress = gatewayAddress;
            _gatewayPort = gatewayPort;
            _serialComPort = serialComPort;
            _hartSerial = new HartSerial(_serialComPort);
          
        }

        public int GatewayPort
        {
            get { return _gatewayPort; }
        }

        public string SerialComPort
        {
            get { return _serialComPort; }
        }

        public HartSerial HartSerial {
            get { return _hartSerial; }
        }

        public void Start()
        {

            _hartSerial.Open();
            _hartSerial.ReconnectOnError = true;

            IPAddress ipAddressToListen;
            if (_gatewayAddress == "localhost")
                ipAddressToListen = IPAddress.Any;
            else
                ipAddressToListen = GetIPAddress(_gatewayAddress);

            _gatewayListener = new TcpListener(ipAddressToListen, GatewayPort);
            _gatewayListener.Start();

            _acceptTcpClientsThread = new Thread(AccepTcpClientsThreadFunction);
            _acceptTcpClientsThread.Name = "AcceptClientsThread";
            _acceptTcpClientsThread.IsBackground = true;
            _isRunning = true;
            _acceptTcpClientsThread.Start();

        }

        public void Stop()
        {
            _isRunning = false;

            var clients = _hartClients.ToArray();

            if (_gatewayListener != null)
            {
                UnlockAcceptTcpClient();
                _gatewayListener.Stop();
            }

            foreach (var client in clients)
            {
                client.Value.Stop();
            }

            _hartSerial.Close();


        }

        private void UnlockAcceptTcpClient()
        {
            TcpClient dummyClient = new TcpClient();
            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"),
                ((IPEndPoint)_gatewayListener.LocalEndpoint).Port);
            dummyClient.Connect(endpoint);
            dummyClient.Close();

        }

        #region AcceptThread
        private Thread _acceptTcpClientsThread;
        private int _waitCounter;
        private void AccepTcpClientsThreadFunction()
        {

            while (_isRunning)
            {
                _waitCounter++;

                TcpClient hartTcpClient = _gatewayListener.AcceptTcpClient();
                var hartClient = new HartClient(this, hartTcpClient);
                hartClient.Start();
            }

        }

        #endregion

        #region Hart Client Collection

        private Dictionary<int, HartClient> _hartClients = new Dictionary<int, HartClient>();

        public void AddHartClientToGateway(HartClient HartClient)
        {
            _hartClients.Add(HartClient.Id, HartClient);
        }

        public void RemoveHartClientFromGateway(HartClient HartClient)
        {
            var removed = _hartClients.Remove(HartClient.Id);
            if (!removed)
            {

            }
        }


        #endregion

        public IPAddress GetIPAddress(string hostname)
        {

            var host = Dns.GetHostEntry(hostname);

            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }

            throw new ArgumentException("hostname  not found " + hostname);
        }


    }


}
