using System;

namespace HartIPGateway.HartIpGateway
{

    public interface IAddress
    {
        byte[] ToByteArray();
        void SetNextByte(byte nextByte);

        byte this[int index] { get; }
    }

    public class LongAddress : IAddress
    {
        private byte _currentAddressIndex;

        private byte _manufacturerIdentificationCode;
        private byte _manufacturerDeviceTypeCode;
        private readonly byte[] _deviceIdentificationNumber;

        public LongAddress(byte manufacturerIdentificationCode, byte manufacturerDeviceTypeCode, byte[] deviceIdentificationNumber)
        {
            if (deviceIdentificationNumber.Length != 3)
                throw new ArgumentException();

            _currentAddressIndex = 0;

            _manufacturerIdentificationCode = manufacturerIdentificationCode;
            _manufacturerDeviceTypeCode = manufacturerDeviceTypeCode;
            _deviceIdentificationNumber = deviceIdentificationNumber;
        }

        public byte[] ToByteArray()
        {
            byte[] address = new byte[5];
            address[0] = GetManufacturerIdentificationCode();
            address[1] = _manufacturerDeviceTypeCode;
            address[2] = _deviceIdentificationNumber[0];
            address[3] = _deviceIdentificationNumber[1];
            address[4] = _deviceIdentificationNumber[2];
            return address;
        }

        public void SetNextByte(byte nextByte)
        {
            if (_currentAddressIndex == 0)
                _manufacturerIdentificationCode = nextByte;
            if (_currentAddressIndex == 1)
                _manufacturerDeviceTypeCode = nextByte;
            if (_currentAddressIndex == 2)
                _deviceIdentificationNumber[0] = nextByte;
            if (_currentAddressIndex == 3)
                _deviceIdentificationNumber[1] = nextByte;
            if (_currentAddressIndex == 4)
                _deviceIdentificationNumber[2] = nextByte;

            ++_currentAddressIndex;
        }

        public static LongAddress Empty
        {
            get
            {
                return new LongAddress(0, 0, new byte[3]);
            }
        }

        public byte this[int index]
        {
            get
            {
                if (index == 0)
                    return GetManufacturerIdentificationCode();
                if (index == 1)
                    return _manufacturerDeviceTypeCode;
                if (index == 2)
                    return _deviceIdentificationNumber[0];
                if (index == 3)
                    return _deviceIdentificationNumber[1];
                if (index == 4)
                    return _deviceIdentificationNumber[2];

                throw new IndexOutOfRangeException();
            }
        }

        private byte GetManufacturerIdentificationCode()
        {
            return _manufacturerIdentificationCode;
        }
    }

    public class ShortAddress : IAddress
    {
        private byte _pollingAddress;

        public ShortAddress(byte pollingAddress)
        {
            if (pollingAddress > 15 || pollingAddress < 0)
                throw new ArgumentException();

            _pollingAddress = pollingAddress;
        }

        public byte[] ToByteArray()
        {
            return new[] { GetPollingAddress() };
        }

        public void SetNextByte(byte nextByte)
        {
            _pollingAddress = nextByte;
        }

        public byte this[int index]
        {
            get
            {
                if (index == 0)
                    return GetPollingAddress();

                throw new IndexOutOfRangeException();
            }
        }

        public static ShortAddress Empty
        {
            get { return new ShortAddress(0); }
        }

        private byte GetPollingAddress()
        {
            return _pollingAddress;
        }
    }

    public class Command
    {
        public byte[] ResponseCode { get; set; }
        public int PreambleLength { get; set; }
        public HartDelimiter StartDelimiter { get; set; }
        public IAddress Address { get; set; }
        public byte CommandNumber { get; set; }
        public byte[] Data { get; set; }

        private static byte MasterToSlaveStartDelimiter
        {
            get
            {
                return 0x82; // 2
            }
        }

        public static byte SlaveToMasterStartDelimiter
        {
            get { return 6; }
        }

        public Command()
        {
        }

        public Command(int preambleLength, IAddress address, byte commandNumber, byte responseCode, byte deviceStatus, byte[] data, FrameType frametype, AddressType addressType) :
                this(preambleLength, address, commandNumber, new byte[] { responseCode, deviceStatus }, data)
        {

            var responseDelimiter = new HartDelimiter(frametype, addressType);
            this.StartDelimiter = responseDelimiter;

        }

        public Command(int preambleLength, IAddress address, byte commandNumber, byte[] responseCode, byte[] data)
        {
            PreambleLength = preambleLength;
            Address = address;
            CommandNumber = commandNumber;
            Data = data;
            ResponseCode = responseCode;
            StartDelimiter = new HartDelimiter(MasterToSlaveStartDelimiter);
        }

        public static Command Zero()
        {
            return Zero(0, 20);
        }

        public static Command Zero(int preambleLength)
        {
            return Zero(0, preambleLength);
        }

        public static Command Zero(byte pollingAddress)
        {
            return Zero(pollingAddress, 20);
        }

        public static Command Zero(byte pollingAddress, int preambleLength)
        {
            return new Command(preambleLength, new ShortAddress(pollingAddress), 0, new byte[0], new byte[0])
            {
                StartDelimiter = new HartDelimiter(2)
            };
        }

        public bool IsChecksumCorrect(byte checksum)
        {
            return CalculateChecksum() == checksum;
        }

        public Byte[] ToByteArray()
        {
            byte[] commandAsByteArray = BuildByteArray();

            commandAsByteArray[commandAsByteArray.Length - 1] = CalculateChecksum();

            return commandAsByteArray;
        }

        private byte[] BuildByteArray()
        {
            const int SIZE_OF_START_DELIMITER = 1;
            const int SIZE_OF_COMMAND_NUMBER = 1;
            const int SIZE_OF_DATA_BYTE_COUNT = 1;
            const int SIZE_OF_CHECKSUM = 1;

            int commandLength = PreambleLength + Data.Length + ResponseCode.Length + Address.ToByteArray().Length +
                                SIZE_OF_START_DELIMITER + SIZE_OF_COMMAND_NUMBER +
                                SIZE_OF_DATA_BYTE_COUNT + SIZE_OF_CHECKSUM;
            var commandAsByteArray = new byte[commandLength];

            int currentIndex = 0;
            for (int i = 0; i < PreambleLength; ++i)
            {
                commandAsByteArray[currentIndex] = 255;
                currentIndex++;
            }
            commandAsByteArray[currentIndex] = StartDelimiter.Data;
            currentIndex += SIZE_OF_START_DELIMITER;
            CopyArrayInArray(commandAsByteArray, Address.ToByteArray(), currentIndex);
            currentIndex += Address.ToByteArray().Length;
            commandAsByteArray[currentIndex] = CommandNumber;
            currentIndex += SIZE_OF_COMMAND_NUMBER;
            commandAsByteArray[currentIndex] = (byte)(Data.Length + ResponseCode.Length);
            currentIndex += SIZE_OF_DATA_BYTE_COUNT;
            CopyArrayInArray(commandAsByteArray, ResponseCode, currentIndex);
            currentIndex += ResponseCode.Length;
            CopyArrayInArray(commandAsByteArray, Data, currentIndex);

            return commandAsByteArray;
        }

        private static void CopyArrayInArray(byte[] destination, byte[] source, int offset)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                destination[i + offset] = source[i];
            }
        }

        internal byte CalculateChecksum()
        {
            byte[] data = BuildByteArray();
            return HartChecksum(data, PreambleLength);
        }

        public static byte HartChecksum(byte[] data, int starIndex)
        {
            byte checksum = 0;
            for (int i = starIndex; i < data.Length - 1; ++i)
            {
                checksum ^= data[i];
            }
            return checksum;
        }

    }

    public class HartCommandParser
    {
        private enum ReceiveState
        {
            NotInCommand,
            Preamble,
            StartDelimiter,
            Address,
            Command,
            DataLength,
            Data,
            Checksum
        }




        private ReceiveState _currentReceiveState = ReceiveState.NotInCommand;
        private Command _currentCommand;
        private int _currentIndex;
        private readonly bool parsePreamble;

        public HartCommandParser(bool parsePreamble)

        {
            this.parsePreamble = parsePreamble;
            if (!this.parsePreamble)
            {
                _currentReceiveState = ReceiveState.Preamble;
            }
        }

        public event Action<Command> CommandComplete;

        public void ParseNextBytes(Byte[] data)
        {
            for (int i = 0; i < data.Length; ++i)
            {
                ParseByte(data[i]);
            }
        }

        private void ParseByte(byte data)
        {
            switch (_currentReceiveState)
            {
                case ReceiveState.NotInCommand:
                    if (data == 0xFF)
                    {
                        _currentReceiveState = ReceiveState.Preamble;
                        ParsePreamble(data);
                    }
                    break;
                case ReceiveState.Preamble:
                    ParsePreamble(data);
                    break;
                case ReceiveState.StartDelimiter:
                    ParseStartDelimiter(data);
                    break;
                case ReceiveState.Address:
                    ParseAddress(data);
                    break;
                case ReceiveState.Command:
                    ParseCommand(data);
                    break;
                case ReceiveState.DataLength:
                    ParseDataLength(data);
                    break;
                case ReceiveState.Data:
                    ParseData(data);
                    break;
                case ReceiveState.Checksum:
                    ParseChecksum(data);
                    break;
            }
        }

        private void ParseCommand(byte data)
        {
            _currentCommand.CommandNumber = data;
            _currentReceiveState = ReceiveState.DataLength;
            _currentIndex = 0;
        }

        private void ParseChecksum(byte data)
        {
            _currentIndex = 0;
            _currentReceiveState = ReceiveState.NotInCommand;

            if (_currentCommand.IsChecksumCorrect(data))
            {
                OnCommandComplete();
                _currentReceiveState = ReceiveState.NotInCommand;
            }
            else
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    // erro no cálculo do checksum 
                    System.Diagnostics.Debugger.Break();
                }
                _currentCommand.Data = new byte[0];
                CommandComplete(_currentCommand);
            }
        }

        private void OnCommandComplete()
        {
            if (CommandComplete != null)
                CommandComplete(_currentCommand);
        }

        private void ParseData(byte data)
        {
            if (this.startDelimiter.FrameType == FrameType.MasterToFieldDevice)
            {
                ParseDataRequest(data);
            }
            else
            {
                ParseDataResponse(data);
            }
        }

        private void ParseDataResponse(byte data)
        {
            if (_currentIndex < 2)
            {
                _currentCommand.ResponseCode[_currentIndex] = data;
            }
            else
            {
                _currentCommand.Data[_currentIndex - 2] = data;
            }
            _currentIndex++;

            if (_currentIndex == _currentCommand.Data.Length + _currentCommand.ResponseCode.Length)
            {
                _currentReceiveState = ReceiveState.Checksum;
            }
        }

        private void ParseDataRequest(byte data)
        {
            _currentCommand.Data[_currentIndex] = data;


            _currentIndex++;

            if (_currentIndex == _currentCommand.Data.Length)
            {
                _currentReceiveState = ReceiveState.Checksum;
            }
        }

        private void ParseDataLength(byte dataLength)
        {
            if (dataLength == 1)
            {
                Reset();
            }

            if (dataLength == 0)
            {
                _currentCommand.ResponseCode = new byte[0];
                _currentCommand.Data = new byte[0];
                _currentReceiveState = ReceiveState.Checksum;
            }
            else
            {


                if (this.startDelimiter.FrameType == FrameType.MasterToFieldDevice)
                {
                    _currentCommand.Data = new byte[dataLength];
                    _currentCommand.ResponseCode = new byte[0];
                }
                else
                {
                    _currentCommand.Data = new byte[dataLength - 2];
                    _currentCommand.ResponseCode = new byte[2];
                }

                _currentReceiveState = ReceiveState.Data;

            }

            _currentIndex = 0;
        }

        private void ParseAddress(byte data)
        {
            _currentCommand.Address.SetNextByte(data);
            _currentIndex++;

            if (_currentCommand.Address.ToByteArray().Length == _currentIndex)
            {
                _currentReceiveState = ReceiveState.Command;
                _currentIndex = 0;
            }
        }

        HartDelimiter startDelimiter;

        private void ParseStartDelimiter(byte data)
        {

            this.startDelimiter = new HartDelimiter(data);

            if (startDelimiter.AddressType == AddressType.Polling)
                _currentCommand.Address = ShortAddress.Empty;
            else
                _currentCommand.Address = LongAddress.Empty;

            _currentCommand.StartDelimiter = startDelimiter;

            _currentReceiveState = ReceiveState.Address;
            _currentIndex = 0;

        }

        private void ParsePreamble(byte data)
        {
            _currentIndex++;
            if (data != 255)
            {
                _currentCommand = new Command();
                _currentReceiveState = ReceiveState.StartDelimiter;
                if (!this.parsePreamble)
                {
                    _currentCommand.PreambleLength = 0;
                }
                else
                {
                    _currentCommand.PreambleLength = _currentIndex;
                }
                _currentIndex = 0;
                if ((_currentCommand.PreambleLength < 2) && this.parsePreamble)
                {
                    Reset();
                    return;
                }
                ParseByte(data);
            }
        }

        public void Reset()
        {
            _currentCommand = new Command();
            _currentIndex = 0;
            if (this.parsePreamble)
            {
                _currentReceiveState = ReceiveState.NotInCommand;
            }
            else
            {
                _currentReceiveState = ReceiveState.Preamble;
            }
        }
    }
}
