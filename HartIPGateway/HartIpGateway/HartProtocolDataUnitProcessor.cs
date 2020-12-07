using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HartIPGateway.HartIpGateway
{
    /// <summary>
    ///    Processa as mensagens do protocolo Hart e encaminha para instrumento via Serial 
    /// </summary>
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
            commandsImplemented.Add(0, this.Command0ReadUniqueIdentifier);
            commandsImplemented.Add(20, this.Command20ReadLongTag);
            commandsImplemented.Add(31, this.Command31InvalidCommandCheckedByHartHost);

            commandsImplemented.Add(74, this.Command74ReadIOSystemCapabilities);
            commandsImplemented.Add(84, this.Command84ReadSubDeviceIdentitySummary);

        }


        byte[] Command31InvalidCommandCheckedByHartHost(Command requestCommand)
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

        byte[] Command74ReadIOSystemCapabilities(Command requestCommand)
        {
            var commandData = new byte[] {
                    0x02, //Maximum Number of I/O Cards (must be greater then or equal to 1). 
                    0x02, //Maximum Number of Channels per I/O Card (must be greater then or equal to 1). 
                    0x01, //Maximum Number of Sub-Devices Per Channel (must be greater then or equal to 1).
                    0x00, //Number of devices detected (the count includes the I/O system itself). 
                    0x02, //Number of devices detected (the count includes the I/O system itself).
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


        byte[] Command84ReadSubDeviceIdentitySummary(Command requestCommand)
        {

            var requestData = requestCommand.Data;


            var commandData = new byte[] {
                   0x00, //Sub-Device Index (Index 0 returns the I/O System Identity) 
                   0x01, //Sub-Device Index (Index 0 returns the I/O System Identity) 
                   0x00, // I/O Card  
                   0x00, // Channel
                   0x00, //Manufacturer ID 
                   0x11, //Manufacturer ID
                   0x11, //Expanded Device Type Code 
                   0xca, //Expanded Device Type Code 
                   0x33, //Device ID
                   0x00, //Device ID
                   0x2a, //Device ID
                   0x05, //Universal Command Revision level
                         // long tag 
                   0x54,0x54,0x2d,0x31,0x30,0x34,0x3a,0x20,0x54,0x4d,0x54,0x31,0x36,0x32,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                   0x01, // Device Revision
                   0x01, // Device Profile
                   0x00, // Private Label Distributor Code
                   0x00  // Private Label Distributor Code
                    };

            byte responseCode = 0;
            byte deviceStatus = 0;

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
            commandData = EncodeHartText(text,32);
            
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




    }

}
