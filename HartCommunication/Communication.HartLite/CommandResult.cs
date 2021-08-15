﻿using System.Collections.Generic; 
using System.Linq; 

namespace Communication.HartLite
{
    public class CommandResult
    {
        private readonly Command _command;

        public byte CommandNumber
        {
            get { return _command.CommandNumber; }
        }

        public byte[] Data
        {
            get { return _command.Data; }
        }

        public byte Delimiter
        {
            get { return _command.StartDelimiter; }
        }

        public ResponseCode ResponseCode
        {
            get { return ResponseCode.ToResponseCode(_command.ResponseCode); }
        }

        public IAddress Address
        {
            get { return _command.Address; }
        }

        public int PreambleLength
        {
            get { return _command.PreambleLength; }
        }

        public byte Checksum
        {
            get { return _command.CalculateChecksum(); }
        }

        public byte[] CommandByteArray()
        {
            return _command.ToByteArray();
        }



        internal CommandResult(Command command)
        {
            _command = command;
        }
    }
}