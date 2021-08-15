using System;
using System.Collections;
using System.ComponentModel;
using System.IO.Ports;
using System.Threading;
using System.Collections.Generic;
using System.IO;
//using log4net;

namespace Finaltec.Communication.HartLite
{
    public class HartCommunicationLite
    {
        private static SerialPortWrapper _port;
        private readonly HartCommandParser _parser = new HartCommandParser();
        private AutoResetEvent _waitForResponse;
        private CommandResult _lastReceivedCommand;
        private IAddress _currentAddress;
        private int _numberOfRetries;

        private readonly Queue _commandQueue = new Queue();
        private const double ADDITIONAL_WAIT_TIME_BEFORE_SEND = 5.0;
        private const double ADDITIONAL_WAIT_TIME_AFTER_SEND = 10.0;
        private const double REQUIRED_TRANSMISSION_TIME_FOR_BYTE = 9.1525;

        public event ReceiveHandler Receive;
        
        public int PreambleLength { get; set; }
        
        public int MaxNumberOfRetries { get; set; }
        
        public TimeSpan Timeout { get; set; }
        
        public bool AutomaticZeroCommand { get; set; }

        private object _locker = new object();

        public IAddress Address
        {
            get { return _currentAddress; }
        }

        public HartCommunicationLite(string comPort) : this(comPort, 2)
        {}

        public HartCommunicationLite(string comPort, int maxNumberOfRetries)
        {
            MaxNumberOfRetries = 3;// maxNumberOfRetries;
            PreambleLength = 20;
            Timeout = TimeSpan.FromSeconds(5);
            AutomaticZeroCommand = true;
            _port = new SerialPortWrapper(comPort, 1200, Parity.Odd, 8, StopBits.One);
        }

        public ISerialPortWrapper Port
        {
            get { return _port; }
        }

        public OpenResult Open()
        {
            try
            {
                _parser.CommandComplete += CommandComplete;

                _port.DataReceived += DataReceived;

                _port.RtsEnable = true;
                _port.DtrEnable = false;

                _port.Open();

                _port.RtsEnable = false;
                _port.DtrEnable = true;

                return OpenResult.Opened;
            }
            catch (ArgumentException exception)
            {
                _port.DataReceived -= DataReceived;
                return OpenResult.ComPortNotExisting;
            }
            catch (UnauthorizedAccessException exception)
            {
                _port.DataReceived -= DataReceived;
                return OpenResult.ComPortIsOpenAlreadyOpen;
            }
            catch (Exception exception)
            {
                _port.DataReceived -= DataReceived;
                return OpenResult.UnknownComPortError;
            }
        }

        public CloseResult Close()
        {
            try
            {
                _parser.CommandComplete -= CommandComplete;
                _port.DataReceived -= DataReceived;
                _port.Close();
                _commandQueue.Clear();
                return CloseResult.Closed;
            }
            catch (InvalidOperationException exception)
            {
                return CloseResult.PortIsNotOpen;
            }
        }

        public CommandResult Send(byte command)
        {
            return Send(command, new byte[0]);
        }

        public void ClearQueue()
        {
            _commandQueue.Clear();
        }
        public CommandResult Send(byte command, byte[] data)
        {
            if (AutomaticZeroCommand && command != 0 && !(_currentAddress is LongAddress))
                SendZeroCommand();

            _numberOfRetries = 3;// MaxNumberOfRetries;
            _commandQueue.Enqueue(new Command(PreambleLength, _currentAddress, command, new byte[0], data));

            if (command == 0)
                return SendZeroCommand();

            return ExecuteCommand();
        }

        public CommandResult SendZeroCommand()
        {
            _numberOfRetries = 3;// MaxNumberOfRetries;
            _commandQueue.Enqueue(Command.Zero(PreambleLength));
            return ExecuteCommand();
        }

        public void SwitchAddressTo(IAddress address)
        {
            _currentAddress = address;
            if (Address is ShortAddress)
                Command.PollingAddress = ((ShortAddress)Address).GetAddress(); 
        }
         
        private CommandResult ExecuteCommand()
        {
            if (_commandQueue.Count < 1) return null;
            lock (_commandQueue)
            {
                return SendCommandSynchronous((Command) _commandQueue.Dequeue());
            }
        }

        private bool ShouldRetry()
        {
            return _numberOfRetries-- > 0;
        }

        private bool HasCommunicationError()
        {
            if (_lastReceivedCommand.ResponseCode.FirstByte < 128)
                return false;
            /*
            Log.Warn("Communication error. First bit of response code byte is set.");

            if ((_lastReceivedCommand.ResponseCode.FirstByte & 0x40) == 0x40)
                Log.WarnFormat("Vertical Parity Error - The parity of one or more of the bytes received by the device was not odd.");
            if ((_lastReceivedCommand.ResponseCode.FirstByte & 0x20) == 0x20)
                Log.WarnFormat("Overrun Error - At least one byte of data in the receive buffer of the UART was overwritten before it was read (i.e., the slave did not process incoming byte fast enough).");
            if ((_lastReceivedCommand.ResponseCode.FirstByte & 0x10) == 0x10)
                Log.WarnFormat("Framing Error - The Stop Bit of one or more bytes received by the device was not detected by the UART (i.e. a mark or 1 was not detected when a Stop Bit should have occoured)");
            if ((_lastReceivedCommand.ResponseCode.FirstByte & 0x08) == 0x08)
                Log.WarnFormat("Longitudinal Partity Error - The Longitudinal Partity calculated by the device did not match the Check Byte at the end of the message.");
            if ((_lastReceivedCommand.ResponseCode.FirstByte & 0x02) == 0x02)
                Log.WarnFormat("Buffer Overflow - The message was too long for the receive buffer of the device.");
                */
            return true;
        }

        private CommandResult SendCommandSynchronous(Command requestCommand)
        {
         
            Receive += CommandReceived;
            try
            {
                SendCommand(requestCommand);
                
                if (!_waitForResponse.WaitOne(600,false))
                {
                    Receive -= CommandReceived;

                    if (ShouldRetry())
                        return SendCommandSynchronous(requestCommand);

                    return null;
                }
                Receive -= CommandReceived;

                if (HasCommunicationError())
                    return ShouldRetry() ? SendCommandSynchronous(requestCommand) : _lastReceivedCommand;

                return _lastReceivedCommand;
            }
            catch (Exception exception)
            {
                Receive -= CommandReceived;

                if (ShouldRetry())
                    return SendCommandSynchronous(requestCommand);

                return null;
            }
        }

        private void SendCommand(Command command)
        {
            lock (_locker)
            {
                _waitForResponse = new AutoResetEvent(false);
                _parser.Reset();

                if (command.Address == null)
                    command.Address = _currentAddress;

                byte[] bytesToSend = command.ToByteArray();

                Thread.Sleep(100);

                _port.DtrEnable = false;
                _port.RtsEnable = true;

                Thread.Sleep(Convert.ToInt32(ADDITIONAL_WAIT_TIME_BEFORE_SEND));

                DateTime startTime = DateTime.Now;

                _port.Write(bytesToSend, 0, bytesToSend.Length);

                SleepAfterSend(bytesToSend.Length, startTime);
                _port.RtsEnable = false;
                _port.DtrEnable = true;

                //using (var fw = new StreamWriter(@"\sd card\hart.txt", true))
                //{
                //    fw.WriteLine("TX:" + BitConverter.ToString(bytesToSend));
                //}
          
            }
           // GetData();
        }

        private void CommandReceived(object sender, CommandResult args)
        {
            _lastReceivedCommand = args;
            _waitForResponse.Set();
        }

        private void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            GetData();
        }

        private static void SleepAfterSend(int dataLength, DateTime startTime)
        {
            TimeSpan waitTime = CalculateWaitTime(dataLength, startTime);
            if (waitTime.Milliseconds > 0)
                Thread.Sleep(waitTime.Milliseconds);
        }

        private void GetData()
        {
            _port.DataReceived -= DataReceived;
            var received = new List<Byte>();
            Thread.Sleep(100);
            var read = true;
            while (read)
            {
                if (_port.BytesToRead > 0)
                {
                    var by = _port.ReadByte();
                    if (by != -1)
                    {
                        received.Add((byte)by);
                    }
                    else
                    {
                        read = false;
                    }
                }
                else
                {
                    read = false;
                }
            }
            //using (var fw = new StreamWriter(@"\sd card\hart.txt", true))
            //{
            //    fw.WriteLine("RX:" + BitConverter.ToString(received.ToArray()));
            //}
            _parser.ParseNextBytes(received.ToArray());
            _port.DataReceived += DataReceived;
        }

        private static TimeSpan CalculateWaitTime(int dataLength, DateTime startTime)
        {
            TimeSpan requiredTransmissionTime = TimeSpan.FromMilliseconds(Convert.ToInt32(REQUIRED_TRANSMISSION_TIME_FOR_BYTE * dataLength + ADDITIONAL_WAIT_TIME_AFTER_SEND));
            return startTime + requiredTransmissionTime - DateTime.Now;
        }

        private void CommandComplete(Command command)
        {
            if(command.CommandNumber == 0)
            {
                try
                {
                    _currentAddress = new LongAddress(command.Data[1], command.Data[2], new[] { command.Data[9], command.Data[10], command.Data[11] });
                }
                catch (Exception)
                {
                }
            }

            if (Receive != null)
                Receive(this, new CommandResult(command));
        }
    }
}