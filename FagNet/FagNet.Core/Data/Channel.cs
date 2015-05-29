using System.Linq;
using FagNet.Core.Constants;
using FagNet.Core.Constants.Packets;
using FagNet.Core.Network;

namespace FagNet.Core.Data
{
    public class Channel
    {
        public ushort ID { get; set; }
        public string Name { get; set; }
        public EServerType ServerType { get; set; }

        private readonly PlayerCollection _players = new PlayerCollection();
        public PlayerCollection Players
        {
            get { return _players; }
        }

        public void Join(Player plr)
        {
            _players.TryAdd(plr.Session.Guid, plr);
            plr.Channel = this;

            if (ServerType != EServerType.Chat) return;
            var ack = new Packet(EChatPacket.SChannelPlayerJoinedAck);
            ack.Write((uint)ID);
            ack.WriteChatUserData(plr);
            Broadcast(ack, plr.AccountID);
        }

        public void Leave(Player plr)
        {
            Player tmp;
            _players.TryRemove(plr.Session.Guid, out tmp);
            plr.Channel = null;

            if (ServerType != EServerType.Chat) return;
            var ack = new Packet(EChatPacket.SChannelPlayerLeftAck);
            ack.Write((uint)ID);
            ack.Write(plr.AccountID);
            Broadcast(ack);
        }

        public void Broadcast(byte[] packet, ulong exclude = 0)
        {
            foreach (var player in _players.Values.Where(player => player.AccountID != exclude))
                player.Session.Send(packet);
        }

        public void Broadcast(Packet packet, ulong exclude = 0)
        {
            Broadcast(packet.GetData(), exclude);
        }
    }
}
