using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FagNet.Core.Data;
using FagNet.Core.Network;

namespace FagNet.Core.Plugin
{
    public abstract class GamePlugin
    {
        public string Name { get; set; }

        public virtual bool OnPacket(TcpSession session, Packet packet)
        {
            return false;
        }
        public virtual bool OnCreateRoom(Player plr, Room room)
        {
            return false;
        }

        public virtual bool RoomTick(Room room)
        {
            return false;
        }

        public virtual void OnBuyItem(Player plr, List<Item> itemsToBuy)
        {
        }

        public virtual string OnAdminAction(Player sender, string[] args)
        {
            return "";
        }

        public virtual bool OnBeginRound(Player plr, Room room)
        {
            return false;
        }

        public virtual bool OnReadyRound(Player plr, Room room)
        {
            return false;
        }
    }
}
