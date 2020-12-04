using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HartIPGateway.HartIpGateway
{
    public class ByteConverterUtil
    {

        static public uint ToUint32(byte byte0, byte byte1, byte byte2, byte byte3)
        {
            uint value;

            if (BitConverter.IsLittleEndian)
                value = BitConverter.ToUInt32(new byte[4] { byte0, byte1, byte2, byte3 }, 0);
            else
                value = BitConverter.ToUInt32(new byte[4] { byte3, byte2, byte1, byte0 }, 0);

            return value;
        }

        static public ushort ToUint16(byte byteLo, byte byteHi)
        {
            ushort value;

            if (BitConverter.IsLittleEndian)
                value = BitConverter.ToUInt16(new byte[2] { byteLo, byteHi }, 0);
            else
                value = BitConverter.ToUInt16(new byte[2] { byteHi, byteLo }, 0);

            return value;
        }

        public static Byte LoByte(UInt16 nValue)
        {
            return (Byte)(nValue & 0xFF);
        }
        public static Byte HiByte(UInt16 nValue)
        {
            return (Byte)(nValue >> 8);
        }
    }
}
