using System;
using System.Net;

namespace FagNet.Core.Data
{
    public struct SServer
    {
        public UInt16 ID { get; set; }
        public byte Type { get; set; }
        public string Name { get; set; }
        public UInt16 PlayerLimit { get; set; }
        public UInt16 PlayersOnline { get; set; }
        public IPAddress IP { get; set; }
        public UInt16 Port { get; set; }

        public bool Online { get; set; }
    }
}
