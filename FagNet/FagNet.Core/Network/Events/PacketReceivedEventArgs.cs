using System;

namespace FagNet.Core.Network.Events
{
    public class PacketReceivedEventArgs : EventArgs
    {
        public Packet Packet { get; private set; }
        public TcpSession Session { get; private set; }

        public PacketReceivedEventArgs()
        {

        }
        public PacketReceivedEventArgs(TcpSession session, byte[] data)
        {
            Session = session;
            Packet = new Packet(data);
        }
        public PacketReceivedEventArgs(TcpSession session, Packet packet)
        {
            Session = session;
            Packet = packet;
        }
    }
}
