using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HartIPGateway.HartIpGateway
{
    public class ByteConverterUtil
    {

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
