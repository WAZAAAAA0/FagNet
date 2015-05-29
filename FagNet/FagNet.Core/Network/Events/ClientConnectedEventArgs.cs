using System;

namespace FagNet.Core.Network.Events
{
    public class ClientConnectedEventArgs : EventArgs
    {
        public TcpSession Session { get; private set; }

        public ClientConnectedEventArgs()
        {

        }
        public ClientConnectedEventArgs(TcpSession session)
        {
            Session = session;
        }
    }
}
