using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Threading;
using log4net;


namespace Communication.HartLite
{
    public class HartCommunicationLite
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(HartCommunicationLite));
        private readonly ISerialPortWrapper _port;
        private readonly HartCommandParser _parser = new HartCommandParser();
        private AutoResetEvent _waitForResponse;
        private CommandResult _lastReceivedCommand;
        private IAddress _currentAddress;
        private int _numberOfRetries;

        private readonly Queue _commandQueue = new Queue();

        private const double ADDITIONAL_WAIT_TIME_BEFORE_SEND = 5.0;
        private const double ADDITIONAL_WAIT_TIME_AFTER_SEND = 10.0;
        private const double REQUIRED_TRANSMISSION_TIME_FOR_BYTE = 9.1525;

        /// <summary>
        /// Raises the event if a command is completed receive. 
        /// </summary>
        public event ReceiveHandler Receive;
        /// <summary>
        /// Gets or sets the length of the preamble.
        /// </summary>
        /// <value>The length of the preamble.</value>
        public int PreambleLength { get; set; }
        /// <summary>
        /// Gets or sets the max number of retries.
        /// </summary>
        /// <value>The max number of retries.</value>
        public int MaxNumberOfRetries { get; set; }
        /// <summary>
        /// Gets or sets the timeout.
        /// </summary>
        /// <value>The timeout.</value>
        public TimeSpan Timeout { get; set; }
        public bool AutomaticZeroCommand { get; set; }

        public IAddress Address
        {
            get { return _currentAddress; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HartCommunicationLite"/> class.
        /// </summary>
        /// <param name="comPort">The COM port.</param>
        public HartCommunicationLite(string comPort) : this(comPort, 2)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="HartCommunicationLite"/> class.
        /// </summary>
        /// <param name="comPort">The COM port.</param>
        /// <param name="maxNumberOfRetries">The max number of retries.</param>
        public HartCommunicationLite(string comPort, int maxNumberOfRetries)
        {
            MaxNumberOfRetries = maxNumberOfRetries;
            PreambleLength = 10;
            Timeout = TimeSpan.FromSeconds(4);
            AutomaticZeroCommand = true;

            _port = new SerialPortWrapper(comPort, 1200, Parity.Odd, 8, StopBits.One);
        }

        /// <summary>
        /// Gets the port.
        /// </summary>
        /// <value>The port.</value>
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
                Log.Warn("Cannot open port.", exception);
                return OpenResult.ComPortNotExisting;
            }
            catch (UnauthorizedAccessException exception)
            {
                _port.DataReceived -= DataReceived;
                Log.Warn("Cannot open port.", exception);
                return OpenResult.ComPortIsOpenAlreadyOpen;
            }
            catch (Exception exception)
            {
                _port.DataReceived -= DataReceived;
                Log.Warn("Cannot open port.", exception);
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
                Log.Warn("Cannot close port.", exception);
                return CloseResult.PortIsNotOpen;
            }
        }

        public byte[] SendRaw(byte[] hartFrameDatawithoutPreamble, int preambleLeghtSize)
        {
            Receive += CommandReceived;
            try
            {
              
                SendRawCommnand(hartFrameDatawithoutPreamble, preambleLeghtSize);
                if (!_waitForResponse.WaitOne((int)Timeout.TotalMilliseconds,false))
                {
                    Receive -= CommandReceived;

                    if (ShouldRetry())
                    {
                        return SendRaw(hartFrameDatawithoutPreamble, preambleLeghtSize);
                    }

                    return null;
                }

                Receive -= CommandReceived;

                if (HasCommunicationError())
                {
                    if (ShouldRetry())
                    {
                        SendRawCommnand(hartFrameDatawithoutPreamble,10);
                        return LastReceivedResponse();
                    }
                }

                var respBytes = LastReceivedResponse();

                return respBytes;

            }
            catch (Exception exception)
            {
                Log.Error("Unexpected exception!", exception);
                Receive -= CommandReceived;

                if (ShouldRetry())
                    return SendRaw(hartFrameDatawithoutPreamble,10);

                return null;
            }


        }

        private byte[] LastReceivedResponse()
        {
            var preambleLength = _lastReceivedCommand.PreambleLength;
            var fullResponse = _lastReceivedCommand.CommandByteArray();
            var respSize = fullResponse.Length - preambleLength;
            var responseNoPreamble = new byte[respSize];
            Array.Copy(fullResponse, preambleLength, responseNoPreamble,0, respSize);
            return responseNoPreamble;
        }

        private void SendRawCommnand(byte[] hartFrameDatawithoutPreamble, int preambleLeghtSize)
        {
            _waitForResponse = new AutoResetEvent(false);
            _parser.Reset();

            var bytesToSendList = new List<byte>();

            var usedPreambleLenght = PreambleLength;

            if (usedPreambleLenght!= preambleLeghtSize)
            {
                usedPreambleLenght= preambleLeghtSize;
            }

            for (int i = 0; i < usedPreambleLenght; i++)
            {
                bytesToSendList.Add(0xff);
            }
            bytesToSendList.AddRange(hartFrameDatawithoutPreamble);

            byte[] bytesToSend = bytesToSendList.ToArray();

            _port.DtrEnable = false;
            _port.RtsEnable = true;

            Thread.Sleep(Convert.ToInt32(ADDITIONAL_WAIT_TIME_BEFORE_SEND));

            DateTime startTime = DateTime.Now;

            Log.Debug(string.Format("Data sent to {1}: {0}", BitConverter.ToString(bytesToSend), _port.PortName));
            _port.Write(bytesToSend, 0, bytesToSend.Length);

             SleepAfterSendSync();
            _port.RtsEnable = false;
            _port.DtrEnable = true;
        }

        public CommandResult Send(byte command)
        {
            return Send(command, new byte[0]);
        }

        public CommandResult Send(byte command, byte[] data)
        {
            if (AutomaticZeroCommand && command != 0 && !(_currentAddress is LongAddress))
                SendZeroCommand();

            _numberOfRetries = MaxNumberOfRetries;
            _commandQueue.Enqueue(new Command(PreambleLength, _currentAddress, command, new byte[0], data));

            if (command == 0)
                return SendZeroCommand();

            return ExecuteCommand();
        }

        public CommandResult SendZeroCommand()
        {
            _numberOfRetries = MaxNumberOfRetries;
            _commandQueue.Enqueue(Command.Zero(PreambleLength));
            return ExecuteCommand();
        }





        public void SwitchAddressTo(IAddress address)
        {
            _currentAddress = address;
        }



        private CommandResult ExecuteCommand()
        {
            if (_commandQueue.Count < 1) return null;
            lock (_commandQueue)
            {
                return SendCommandSynchronous((Command)_commandQueue.Dequeue());
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
                Log.Error("Unexpected exception!", exception);
                Receive -= CommandReceived;

                if (ShouldRetry())
                    return SendCommandSynchronous(requestCommand);

                return null;
            }
        }

        private void SendCommand(Command command)
        {
            
            _waitForResponse = new AutoResetEvent(false);
            _parser.Reset();

            if (command.Address == null)
                command.Address = _currentAddress;

            byte[] bytesToSend = command.ToByteArray();

            _port.DtrEnable = false;
            _port.RtsEnable = true;

            Thread.Sleep(Convert.ToInt32(ADDITIONAL_WAIT_TIME_BEFORE_SEND));

            DateTime startTime = DateTime.Now;
            Log.Debug(string.Format("Data sent to {1}: {0}", BitConverter.ToString(bytesToSend), _port.PortName));
            _port.Write(bytesToSend, 0, bytesToSend.Length);
                      
            SleepAfterSendSync();

            _port.RtsEnable = false;
            _port.DtrEnable = true;
           
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

        private void SleepAfterSendSync()
        {
            while (_port.BytesToWrite > 0)
            {
                Thread.Sleep(1);
            }

            Thread.Sleep((int)ADDITIONAL_WAIT_TIME_AFTER_SEND);
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
            Thread.Sleep(10);
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
            if (command.CommandNumber == 0)
            {
                //PreambleLength = command.PreambleLength;

                _currentAddress = new LongAddress(command.Data[1], command.Data[2], new[] { command.Data[9], command.Data[10], command.Data[11] });
            }

            if (Receive != null)
                Receive(this, new CommandResult(command));
        }
    }
}
