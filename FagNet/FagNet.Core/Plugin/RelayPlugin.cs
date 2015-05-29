using FagNet.Core.Network;

namespace FagNet.Core.Plugin
{
    public abstract class RelayPlugin
    {
        public abstract bool HandlePacket(TcpSession session, Packet packet);
    }
}
