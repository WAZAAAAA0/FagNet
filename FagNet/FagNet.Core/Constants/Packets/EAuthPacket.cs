namespace FagNet.Core.Constants.Packets
{
    public enum EAuthPacket : byte
    {
        CLoginReq = 0x06,
        CAuthReq = 0x0A,

        SServerlistAck = 0x07,
        SAuthAck = 0x0B,
    }
}
