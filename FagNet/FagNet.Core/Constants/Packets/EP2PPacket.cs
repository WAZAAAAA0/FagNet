namespace FagNet.Core.Constants.Packets
{
    public enum EP2PPacket : byte
    {
        //CONNECT_SYN = 0x01,
        //CONNECT_SYN_ACK = 0x02,
        //CONNECT_ACK = 0x03,
        //CONNECT_FIN = 0x04,
        //CONNECT_PUNCH = 0x5,
        //UNK1 = 0x06,
        //KEEP_ALIVE_REQ = 0x07,
        //KEEP_ALIVE_ACK = 0x08,
        PLAYER_SPAWN_REQ = 0x0B,
        PLAYER_SPAWN_ACK = 0x0C,
        //PLAYER_DAMAGE = 0x0F,
        //PLAYER_REMOTE_DAMAGE = 0x10,
        //PLAYER_POSITION = 0x22,
        //PLAYER_ANIMATION_STATE = 0x23,
        //PLAYER_MESSAGE = 0x30,
        //PLAYER_ONOFF = 0x27,
        //PLAYER_BIND_PROPERTIES = 0x28,
        //PLAYER_BIND_ANIMATION = 0x29,
        //PLAYER_WALL_CREATION = 0x3C,
        //FUMBI_REBOUND = 0x32,
        //FUMBI_POSITION = 0x33,
    }
}
