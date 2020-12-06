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
        }



        byte[] Command20ReadLongTag(Command requestCommand)
        {
            byte[] commandData;

            byte responseCode = 0;
            byte deviceStatus = 0;

            Encoding iso = Encoding.GetEncoding("ISO-8859-1");
            var text = "PRESYS HART GATEWAY";
            text = text.PadRight(32);
            commandData = iso.GetBytes(text);
            var responseCommand = new Command(0, requestCommand.Address, requestCommand.CommandNumber, responseCode, deviceStatus, commandData, FrameType.FieldDeviceToMaster);
            var responseBytes = responseCommand.ToByteArray();

            return responseBytes;
        }

        byte[] Command0ReadUniqueIdentifier(Command requestCommand)
        {

            var commandData = new byte[] {
                0xfe, //Expansion Code
                0xf9, //Expanded Device Type
                0x82, //Expanded Device Type
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

            var responseCommand = new Command(0, requestCommand.Address, requestCommand.CommandNumber, responseCode, deviceStatus, commandData, FrameType.FieldDeviceToMaster);
            var responseBytes = responseCommand.ToByteArray();

            return responseBytes;
        }




    }

}
