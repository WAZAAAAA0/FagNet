namespace FagNet.Core.Constants.Packets
{
    public enum EChatPacket : byte
    {
        CKeepAliveAck = 0x01,
        CLoginReq = 0x03,
        CChannelLeaveReq = 0x04,
        CChannelEnterReq = 0x05,
        CMessageReq = 0x0A,
        CChannelListReq = 0x0C,
        CWhisperReq = 0x10,
        CBRSFriendNotifyReq = 0x1C,
        CAddFriendReq = 0x1A,
        CFriendListReq = 0x24,
        CSetDataReq = 0x30,
        CGetDataReq = 0x31,
        CSetStateReq = 0x33,
        CCombiListReq = 0x3A,
        CAddDenyReq = 0x53,
        CRemoveDenyReq = 0x55,
        CDenyListReq = 0x57,

        SLoginAck = 0x02,
        SChannelEnterAck = 0x06,
        SChannelPlayerJoinedAck = 0x07,
        SGetDataAck = 0x32,
        SChannelPlayerLeftAck = 0x08,
        SChannelPlayerListInfoAck = 0x09,
        SMessageAck = 0x0B,
        SWhisperAck = 0x11,
        SAddFriendAck = 0x1B,
        SChannelListAck = 0x0D,
        SFriendListAck = 0x25,
        SBRSFriendNotifyAck = 0x39,
        SCombiListAck = 0x3B,
        SAddDenyAck = 0x54,
        SRemoveDenyAck = 0x56,
        SDenyListAck = 0x58,

    }
}
