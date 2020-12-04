namespace HartIPGateway.HartIpGateway
{
    /// <summary>
    /// HART IP Message class
    /// </summary>
    public static class HARTIPMessage
    {
        /// <summary>
        /// HART UDP/TCP message header size
        /// </summary>
        internal const int HART_MSG_HEADER_SIZE = 8;

        /// <summary>
        ///  HART UDP/TCP version
        /// </summary>
        internal const byte HART_UDP_TCP_MSG_VERSION = 1;

    }


}
