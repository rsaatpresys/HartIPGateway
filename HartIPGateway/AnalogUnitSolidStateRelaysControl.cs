using System;
using System.IO.Ports;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Presys.CE.Modbus;

namespace HartIPGatewayCF
{
    internal static class AnalogUnitSolidStateRelaysControl
    {

        private const int CONTROL_REGISTER = 1697;

        public enum HART_CODES : short
        {
            ENABLE_HART_NO_RESISTOR = 0,
            ENABLE_HART_INTERNAL_RESISTOR = 1,
            ENABLE_MA_ONLY = 4,
            ENABLE_HART_DISABLE_MA = 2
        }

        internal static void EnableHartWithoutInternalResistor()
        {
            var resp = _modbusComm.WriteMultipleRegisters(1, CONTROL_REGISTER, 1, new short[] { (short)HART_CODES.ENABLE_HART_NO_RESISTOR });
            if (!resp)
            {
                Console.WriteLine("Failed to Write CONTROL_REGISTER ENABLE_HART_NO_RESISTOR");
            }
        }

        internal static void EnableHartWithInternalResistor()
        {
            var resp = _modbusComm.WriteMultipleRegisters(1, CONTROL_REGISTER, 1, new short[] { (short)HART_CODES.ENABLE_HART_INTERNAL_RESISTOR });
            if (!resp)
            {
                Console.WriteLine("Failed to Write CONTROL_REGISTER ENABLE_HART_INTERNAL_RESISTOR");
            }
        }

        private static modbus _modbusComm;
        private const int BAUD_RATE = 9600;
        private const int DATA_BITS = 8;
        private const int STOP_BITS = 1;

        internal static bool Open(string portName)
        {
             _modbusComm = new modbus();           
             return _modbusComm.Open(portName, BAUD_RATE, DATA_BITS, Parity.None, (StopBits) STOP_BITS);

        }

        internal static void Close()
        {
            _modbusComm.Close();
        }


    }
}
