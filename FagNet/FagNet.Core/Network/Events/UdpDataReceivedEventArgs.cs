using System;
using System.Net;

namespace FagNet.Core.Network.Events
{
    public class UdpDataReceivedEventArgs : EventArgs
    {
        public byte[] Packet { get; private set; }
        public IPEndPoint IPEndPoint { get; private set; }

        public UdpDataReceivedEventArgs()
        {

        }
        public UdpDataReceivedEventArgs(IPEndPoint ipendpoint, byte[] data)
        {
            IPEndPoint = ipendpoint;
            Packet = data;
        }
    }
}
