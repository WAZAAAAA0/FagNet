namespace FagNet.Core.Constants.Packets
{
    public enum ERelayPacket : byte
    {
        CKeepAliveReq = 0x01,
        CLoginReq = 0x03,
        CJoinTunnelReq = 0x04,
        CUseTunnelReq = 0x05,
        CDetourPacketReq = 0x07,
        CLeaveTunnelReq = 0x0A,

        SResultAck = 0x02,
        SUseTunnelServerReq = 0x05,
        SUseTunnelAck = 0x06,
        SDetourPackettAck = 0x09,
        SLeaveTunnelAck = 0x0B,
    }
}
