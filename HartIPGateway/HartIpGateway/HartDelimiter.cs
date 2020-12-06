namespace HartIPGateway.HartIpGateway
{
    public enum AddressType
    {
        Polling = 0,
        Unique = 1
    }

    public enum PhysicalLayerType
    {
        Asynchronous = 0,
        Synchronous = 1
    }

    public enum FrameType
    {
        BurstFrame = 1,
        MasterToFieldDevice = 2,
        FieldDeviceToMaster = 6
    }


    public class HartDelimiter
    {
        private readonly byte data;

        public int NumberExpansionBytes
        {
            get
            {
                var value = (this.data & 0x60) >> 5;
                return value;
            }
        }
        public AddressType AddressType
        {
            get
            {
                var value = (this.data & 0x80) >> 7;
                return (AddressType)value;
            }
        }
        public PhysicalLayerType PhysicalLayerType
        {
            get
            {
                var value = (this.data & 0x18) >> 3;
                return (PhysicalLayerType)value;
            }
        }
        
        public FrameType FrameType
        {
            get
            {
                var value = (this.data & 0x07) >> 0;
                return (FrameType)value;
            }
        }

        public byte Data
        {
            get
            {
                return this.data;
            }
        }

       
            public HartDelimiter(byte data)
        {
            this.data = data;
        }

    }
}
