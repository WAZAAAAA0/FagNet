using System;
using FagNet.Core.Network;

namespace FagNet.Core.Utils
{
    public class PacketLogger : Logger
    {
        public void Log<T>(Packet p) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
                throw new ArgumentException("T must be an enumerated type");

            lock (_sync)
            {
                try
                {
                    _stream.WriteLine(string.Format("Packet: {0}({1})\r\n{2}", ((T)(object)p.PacketID), p.PacketID.ToString("X2"), BitConverter.ToString(p.GetData())).Replace("-", " "));
                }
                catch (Exception)
                {
                    _stream.WriteLine(string.Format("Packet: ({0})\r\n{1}", p.PacketID.ToString("X2"), BitConverter.ToString(p.GetData())).Replace("-", " "));
                }
                _stream.WriteLine("####################");
                _stream.WriteLine("");
            }
        }
    }
}
