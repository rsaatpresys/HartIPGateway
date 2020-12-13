using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HartIPGateway.HartIpGateway
{
    /// <summary>
    ///    Processa as mensagens do protocolo Hart e encaminha para instrumento via Serial 
    /// </summary>
    /// <remarks>
    /// Funções necessárias para ser implementadas pelo Gateway 
    /// 
    /// Command 38 Reset Configuration Changed Flag 
    /// Command 41 Perform Self Test 
    /// Command 42 Perform Device Reset 
    /// Command 48 Read Additional Device Status 
    /// Command 74 Read I/O System Capabilities 
    /// Command 75 Poll Sub-Device 
    /// Command 77 Send Command to Sub-Device 
    /// Command 78 Read Aggregated Commands 
    /// Command 83 Reset Device Variable Trim 
    /// Command 84 Read Sub-Device Identity Summary 
    /// Command 85 Read I/O Channel Statistics 
    /// Command 86 Read Sub-Device Statistics 
    /// Command 87 Write I/O System Master Mode 
    /// Command 88 Write I/O System Retry Count 
    /// Command 89 Set Real-Time Clock 
    /// Command 94 Read I/O System Client-Side Communication Statistics 
    /// 
    /// </remarks>
    public class HartProtocolDataUnitProcessor
    {
        public ProcessorModeType ProcessorMode { get; }
        public HartSerial HartSerial { get; }
        public HartCommandParser commandParser { get; }

        public HartProtocolDataUnitProcessor(ProcessorModeType processorMode, HartSerial hartSerial)
        {
            ProcessorMode = processorMode;
            HartSerial = hartSerial;
            this.commandParser = new HartCommandParser(parsePreamble: false);
            this.commandParser.CommandComplete += CommandParser_CommandComplete;
            InitializeCommandsDictionary();
        }

        /// <summary>
        ///    modo de processar as mensagens  Hart Enviada para o dispositivo
        /// </summary>
        public enum ProcessorModeType
        {
            /// <summary>
            ///   encaminha mensagens diretamente para rede Hart 
            /// </summary>
            SendSerialNetWork = 0,

            /// <summary>
            ///    Implementa um Hart Gateway 
            /// </summary>
            HartGateWay = 1

        }

        public byte[] ProcessProtocolMessage(byte[] pduMessageRequest)
        {

            commandParser.Reset();
            commandParser.ParseNextBytes(pduMessageRequest);

            if (this.ProcessorMode == ProcessorModeType.SendSerialNetWork)
            {
                var rawResult = this.HartSerial.SendRawCommand(pduMessageRequest);
                return rawResult;
            }
            else
            {
                var requestCommand = LastReceivedCommand;

                if (requestCommand.CommandNumber == 84)
                {
                    int i = 0;
                }

                var response = new byte[0];

                if (this.commandsImplemented.ContainsKey(requestCommand.CommandNumber))
                {
                    var commandFunction = commandsImplemented[requestCommand.CommandNumber];
                    response = commandFunction(requestCommand);
                }
                else
                {
                    int i = 0;
                }

                return response;
            }
        }

        private Command LastReceivedCommand;

        private void CommandParser_CommandComplete(Command obj)
        {
            LastReceivedCommand = obj;
        }

        private Dictionary<int, Func<Command, byte[]>> commandsImplemented;

        private void InitializeCommandsDictionary()
        {
            commandsImplemented = new Dictionary<int, Func<Command, byte[]>>();
            commandsImplemented.Add(00, this.Command0ReadUniqueIdentifier);
            commandsImplemented.Add(13, this.Command13ReadTagDescriptorDate);
            commandsImplemented.Add(20, this.Command20ReadLongTag);
            commandsImplemented.Add(31, this.Command31CheckIfCanQueryExtendedCommand);

            commandsImplemented.Add(74, this.Command74ReadIOSystemCapabilities);
            commandsImplemented.Add(77, this.Command77SendCommandToSubDevice);
            commandsImplemented.Add(84, this.Command84ReadSubDeviceIdentitySummary);

        }


        byte[] Command31CheckIfCanQueryExtendedCommand(Command requestCommand)
        {
            var commandData = new byte[] {
                    0x00
                    };

            byte responseCode = 0;
            byte deviceStatus = 0;

            var responseCommand = new Command(0, requestCommand.Address, requestCommand.CommandNumber, responseCode, deviceStatus, commandData, FrameType.FieldDeviceToMaster, requestCommand.StartDelimiter.AddressType);
            var responseBytes = responseCommand.ToByteArray();

            return responseBytes;
        }

        private byte NumberIOCards = 2; // gateway e um único device 
        private byte NumberChannelsPerIOCard = 2;
        private byte MaxSubDevicesPerChannel = 1;
        private byte NumberDevicesDetected = 2; // gateway e um único device 

        byte[] Command74ReadIOSystemCapabilities(Command requestCommand)
        {
            var commandData = new byte[] {
                    NumberIOCards, //Maximum Number of I/O Cards (must be greater then or equal to 1). 
                    NumberChannelsPerIOCard, //Maximum Number of Channels per I/O Card (must be greater then or equal to 1). 
                    MaxSubDevicesPerChannel, //Maximum Number of Sub-Devices Per Channel (must be greater then or equal to 1).
                    0x00, //Number of devices detected (the count includes the I/O system itself). 
                    NumberDevicesDetected, //Number of devices detected (the count includes the I/O system itself).
                    0x02, // Maximum number of delayed responses supported by I/O System.  Must be at least two.
                    0x01, // Master Mode for communication on channels.  0 = Secondary Master1 = Primary Master(default)
                    0x03  // Retry Count to use when sending commands to a Sub-Device.  Valid range is 2 to 5.  3 retries is default.
                    };

            byte responseCode = 0;
            byte deviceStatus = 0;

            var responseCommand = new Command(0, requestCommand.Address, requestCommand.CommandNumber, responseCode, deviceStatus, commandData, FrameType.FieldDeviceToMaster, requestCommand.StartDelimiter.AddressType);
            var responseBytes = responseCommand.ToByteArray();

            return responseBytes;
        }

        byte[] Command77SendCommandToSubDevice(Command requestCommand)
        {

            var requestData = requestCommand.Data;

            var ioCard = requestData[0];
            var channel = requestData[1];
            var preambleCount = requestData[2];

            var byte0OfAddress = requestData[4];
            var byte0OfAddressMaster = (byte)(requestData[4] | 0x80);

            requestData[4] = byte0OfAddressMaster;

            var requestNoPreamble = BuildRawSerialRequest(requestData);
            
            var responseNoPreamble = this.HartSerial.SendRawCommand(requestNoPreamble);

            bool errorReceivingMessage = false;

            if (responseNoPreamble == null)
            {
                errorReceivingMessage = true;
            }
            else
            {
                if (responseNoPreamble.Length == 0)
                {
                    errorReceivingMessage = true;
                }
            }

            byte[] commandData;
            var commandDataResponse = new List<byte>();

            byte responseCode;
            byte deviceStatus;

            if (errorReceivingMessage)
            {
                commandData = new byte[0];
                responseCode = 2; //Invalid Selection
                deviceStatus = 0;
            }
            else
            {
                commandDataResponse.Add(ioCard);
                commandDataResponse.Add(channel);
                responseNoPreamble[1] = byte0OfAddress;
                var respNoCheckByte = responseNoPreamble.Take(responseNoPreamble.Length - 1);
                commandDataResponse.AddRange(respNoCheckByte);
                commandData = commandDataResponse.ToArray();
                responseCode = 0;
                deviceStatus = 0;
            }

            var responseCommand = new Command(0, requestCommand.Address, requestCommand.CommandNumber, responseCode, deviceStatus, commandData, FrameType.FieldDeviceToMaster, requestCommand.StartDelimiter.AddressType);
            var responseBytes = responseCommand.ToByteArray();

            return responseBytes;
        }

        private byte[] BuildRawSerialRequest(byte[] command77DataRequest)
        {

            var requestNoPreamble = new List<byte>();

            var requestWithoutCheckByte = command77DataRequest.Skip(3).ToArray();

            requestNoPreamble.AddRange(requestWithoutCheckByte);

            var checkByte = Command.HartChecksum(requestWithoutCheckByte, 0);
            requestNoPreamble.Add(checkByte);

            return requestNoPreamble.ToArray();
        }


        byte[] Command84ReadSubDeviceIdentitySummary(Command requestCommand)
        {

            var requestData = requestCommand.Data;

            var subDeviceIndex = ByteConverterUtil.ToUint16(requestData[1], requestData[0]);

            var rawResult = this.HartSerial.Send(0);
            var commandResponse = ParseSerialResponse(rawResult);
            var responseZero = commandResponse.Data;

            rawResult = this.HartSerial.Send(13);
            commandResponse = ParseSerialResponse(rawResult);
            var responseCommand13 = commandResponse.Data;

            var temp = responseCommand13.Take(6).ToArray();

            var tagTxt = UnpackAscii(temp, 6);

            temp = responseCommand13.Skip(6).Take(12).ToArray();

            var tagDescriptorTxt = UnpackAscii(temp, 12);

            var commandDataList = new List<byte>();

            commandDataList.Add(0x00); //Sub-Device Index (Index 0 returns the I/O System Identity) 
            commandDataList.Add(0x01); //Sub-Device Index (Index 0 returns the I/O System Identity) 
            commandDataList.Add(0x00); // I/O Card  
            commandDataList.Add(0x00); // Channel
            commandDataList.Add(0x00); //0x00 Manufacturer ID command 0  byte 17-18 
            var manufacturerByte = responseZero[1];
            commandDataList.Add(manufacturerByte); //0x11 Manufacturer ID
            commandDataList.Add(manufacturerByte); //0x11 Expanded Device Type Code command 0 byte 1-2 
            commandDataList.Add(responseZero[2]); //Expanded Device Type Code 
            commandDataList.Add(responseZero[9]); //Device ID  command 0 byte 9-11
            commandDataList.Add(responseZero[10]);//Device ID
            commandDataList.Add(responseZero[11]); //Device ID
            commandDataList.Add(responseZero[4]); //Universal Command Revision level Command 0 byte-4
                                                  // long tag Command 20 

            var tempTag = tagTxt + " " + tagDescriptorTxt;
            temp = EncodeHartText(tempTag, 30);
            commandDataList.AddRange(temp);

            commandDataList.Add(0x01); // Device Revision
            commandDataList.Add(0x01); // Device Profile
            commandDataList.Add(0x00); // Private Label Distributor Code
            commandDataList.Add(0x00);  // Private Label Distributor Code


            byte responseCode = 0;
            byte deviceStatus = 0;

            var responseCommand = new Command(0, requestCommand.Address, requestCommand.CommandNumber, responseCode, deviceStatus, commandDataList.ToArray(), FrameType.FieldDeviceToMaster, requestCommand.StartDelimiter.AddressType);
            var responseBytes = responseCommand.ToByteArray();

            return responseBytes;
        }


        private Command ParseSerialResponse(byte[] serialResponse)
        {
            var commandParserSerialResult = new HartCommandParser(parsePreamble: true);
            commandParserSerialResult.CommandComplete += CommandParserSerialResult_CommandComplete;
            commandParserSerialResult.Reset();
            commandParserSerialResult.ParseNextBytes(serialResponse);
            var resp = LastReceivedSerialCommand;
            return resp;
        }

        private Command LastReceivedSerialCommand;

        private void CommandParserSerialResult_CommandComplete(Command obj)
        {
            LastReceivedSerialCommand = obj;
        }


        byte[] Command13ReadTagDescriptorDate(Command requestCommand)
        {
            byte[] commandData;

            byte responseCode = 0;
            byte deviceStatus = 0;

            commandData = new byte[] { 0x20, 0xd5, 0x58, 0xd0, 0x44, 0xe0, 0x35, 0x98, 0x08, 0x05, 0x25, 0x20, 0x10, 0x55, 0x89, 0x0c, 0x58, 0x00, 0x16, 0x02, 0x13 };

            var responseCommand = new Command(0, requestCommand.Address, requestCommand.CommandNumber, responseCode, deviceStatus, commandData, FrameType.FieldDeviceToMaster, requestCommand.StartDelimiter.AddressType);
            var responseBytes = responseCommand.ToByteArray();

            return responseBytes;
        }


        byte[] Command20ReadLongTag(Command requestCommand)
        {
            byte[] commandData;

            byte responseCode = 0;
            byte deviceStatus = 0;

            var text = "PRESYS Calibrator Gateway";
            commandData = EncodeHartText(text, 32);

            var responseCommand = new Command(0, requestCommand.Address, requestCommand.CommandNumber, responseCode, deviceStatus, commandData, FrameType.FieldDeviceToMaster, requestCommand.StartDelimiter.AddressType);
            var responseBytes = responseCommand.ToByteArray();

            return responseBytes;
        }

        private byte[] EncodeHartText(string text, int length)
        {
            Encoding iso = Encoding.GetEncoding("ISO-8859-1");
            var textBytes = iso.GetBytes(text);
            var textFormatted = new List<byte>();
            textFormatted.AddRange(textBytes);
            textFormatted.Add(0);
            for (int i = textFormatted.Count; i < length; i++)
            {
                textFormatted.Add(0x00);
            }



            textBytes = textFormatted.ToArray();

            if (textBytes.Length > length)
            {
                throw new InvalidOperationException("Error encoding Hart text max length is " + length);
            }

            return textBytes;

        }

        byte[] Command0ReadUniqueIdentifier(Command requestCommand)
        {

            var commandData = new byte[] {
                0xfe, //Expansion Code
                0xe4, //Expanded Device Type
                0xa1, //Expanded Device Type
                0x05, //Minimum Number of Request Preambles
                0x07, //HART Universal Revision
                0x01, //Device Revision
                0x07, //Device Software Revision
                0x16, //Hardware Rev and Physical Signaling
                0x0c, //Flags 
                0xa0, //Device ID 
                0x00, //Device ID 
                0x6c, //Device ID 
                0x05, //Minimum Number Response Preambles 
                0x08, //Maximum Number Device Variables 
                0x00, //Configuration Change Counter 
                0x00, //Configuration Change Counter 
                0x00, //Extended Device Status
                0x60, //Manufacturer ID  https://support.fieldcommgroup.org/en/support/solutions/articles/8000083841-current-list-of-hart-manufacturer-id-codes
                0xbc, //Manufacturer ID Procomsol 
                0x60, // Private Label 
                0xbc, // Private Label 
                0x04  // Profile I/O System  HCF_SPEC-183 Common Tables Specification Table 57.  Device Profile Codes 
              };

            byte responseCode = 0;
            byte deviceStatus = 0;

            var responseCommand = new Command(0, requestCommand.Address, requestCommand.CommandNumber, responseCode, deviceStatus, commandData, FrameType.FieldDeviceToMaster, requestCommand.StartDelimiter.AddressType);
            var responseBytes = responseCommand.ToByteArray();

            return responseBytes;
        }


        /// <summary>
        /// Unpack the HART packed ASCII string from the specified array
        /// into a null terminated string. 
        /// <note>Only translates the closes multiple of 3 of the packed length.</note>
        /// </summary>
        /// <param name="acResponse">byte[] array containing the packed ASCII string</param>
        /// <param name="cPackedLength">byte number of bytes in the packed string</param>
        /// <returns>String unpacked ASCII string</returns>
        public static String UnpackAscii(byte[] acResponse, byte cPackedLength)
        {
            ushort usIdx;
            ushort usGroupCnt;
            ushort usMaxGroups;    // Number of 4 byte groups to pack.
            ushort usMask;
            ushort[] usBuf = new ushort[4];
            String ascii = String.Empty;
            int iIndex = 0;

            usMaxGroups = (ushort)(cPackedLength / 3);

            for (usGroupCnt = 0; usGroupCnt < usMaxGroups; usGroupCnt++)
            {
                // First unpack 3 bytes into a group of 4 bytes, clearing bits 6 & 7.
                usBuf[0] = (ushort)(acResponse[iIndex] >> 2);
                usBuf[1] = (ushort)(((acResponse[iIndex] << 4) & 0x30) | (acResponse[iIndex + 1] >> 4));
                usBuf[2] = (ushort)(((acResponse[iIndex + 1] << 2) & 0x3C) | (acResponse[iIndex + 2] >> 6));
                usBuf[3] = (ushort)(acResponse[iIndex + 2] & 0x3F);
                iIndex += 3;

                // Now transfer to unpacked area, setting bit 6 to complement of bit 5.
                for (usIdx = 0; usIdx < 4; usIdx++)
                {
                    usMask = (ushort)(((usBuf[usIdx] & 0x20) << 1) ^ 0x40);
                    ascii += (char)(usBuf[usIdx] | usMask);
                }
            }
            return ascii;
        }

    }

}
