using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace FagNet.Core.Data
{
    public class ChannelCollection : ConcurrentDictionary<ushort, Channel>
    {
        public Channel GetChannelByName(string name)
        {
            var res = from chan in Values
                      where chan.Name.Equals(name)
                      select chan;
            var channels = res as IList<Channel> ?? res.ToList();
            return !channels.Any() ? null : channels.First();
        }
    }
}
