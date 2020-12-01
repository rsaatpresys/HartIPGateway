using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HartIPGateway.HartIpGateway
{

    public enum MsgType
    {
        Request = 0,
        Response = 1,
        PublishNotification = 2,
        NAK = 15,
        Reserverd = 256
    }

    public enum MsgIdType
    {
        SessionInitiate = 0,
        SessionClose = 1,
        KeepAlive = 2,
        TokenPassingPDU = 3,
        Discovery = 128,
        Reserved = 256
    }

    public class HartMessageHeader
    {
        private readonly byte[] headerBytes;

        public HartMessageHeader(byte[] headerBytes)
        {
            this.headerBytes = headerBytes.Take(8).ToArray();

            this.Version = headerBytes[0];
            this.MessageType = (MsgType)headerBytes[1];
            this.MessageId = (MsgIdType)headerBytes[2];
            this.StatusCode = headerBytes[3];
            this.SequenceNumber = ByteConverterUtil.ToUint16(headerBytes[5], headerBytes[4]);
            this.ByteCount = ByteConverterUtil.ToUint16(headerBytes[7], headerBytes[6]);
        }

        public HartMessageHeader(byte Version, MsgType MessageType, MsgIdType MessageId, byte StatusCode , ushort SequenceNumber , ushort ByteCount)
        {
            this.headerBytes = new byte[8];
            this.headerBytes[0] = Version;
            this.headerBytes[1] = (byte)MessageType;
            this.headerBytes[2] = (byte)MessageId;
            this.headerBytes[3] = (byte)StatusCode;
            this.headerBytes[4] = ByteConverterUtil.HiByte(SequenceNumber);
            this.headerBytes[5] = ByteConverterUtil.LoByte(SequenceNumber);
            this.headerBytes[6] = ByteConverterUtil.HiByte(ByteCount);
            this.headerBytes[7] = ByteConverterUtil.LoByte(ByteCount);

        }


        public byte[] HeaderBytes
        {
            get
            {
                return this.headerBytes;
            }
        }

        public byte Version { get; private set; }

        public MsgType MessageType { get; private set; }

        public MsgIdType MessageId { get; private set; }

        public byte StatusCode { get; private set; }

        public ushort SequenceNumber { get; private set; }

        public ushort ByteCount { get; private set; }



    }
}
