using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using FagNet.Core.Data;
using FagNet.Core.Database;
using FagNet.Core.Network;
using FagNet.Core.Plugin;

namespace FagNetGame
{
    class PluginManager : GamePlugin
    {
        [ImportMany(typeof(GamePlugin))]
        public List<GamePlugin> Plugins = new List<GamePlugin>();
        
        public void Load()
        {
            var path = AppDomain.CurrentDomain.BaseDirectory;
            path = Path.Combine(path, "Plugins");
            if (!Directory.Exists(path))
                return;
            var catalog = new DirectoryCatalog(path);
            var container = new CompositionContainer(catalog);
            container.ComposeParts(this);
        }

        public override bool OnPacket(TcpSession session, Packet packet)
        {
            var cancel = false;
            foreach (var plugin in Plugins)
            {
                if (plugin.OnPacket(session, packet))
                    cancel = true;
            }
            return cancel;
        }

        public override bool OnCreateRoom(Player plr, Room room)
        {
            var cancel = false;
            foreach (var plugin in Plugins)
            {
                if (plugin.OnCreateRoom(plr, room))
                    cancel = true;
            }
            return cancel;
        }

        public override bool RoomTick(Room room)
        {
            var cancel = false;
            foreach (var plugin in Plugins)
            {
                if (plugin.RoomTick(room))
                    cancel = true;
            }
            return cancel;
        }

        public override void OnBuyItem(Player plr, List<Item> itemsToBuy)
        {
            foreach (var plugin in Plugins)
                plugin.OnBuyItem(plr, itemsToBuy);
        }

        public override bool OnBeginRound(Player plr, Room room)
        {
            var cancel = false;
            foreach (var plugin in Plugins)
            {
                if (plugin.OnBeginRound(plr, room))
                    cancel = true;
            }
            return cancel;
        }

        public override bool OnReadyRound(Player plr, Room room)
        {
            var cancel = false;
            foreach (var plugin in Plugins)
            {
                if (plugin.OnReadyRound(plr, room))
                    cancel = true;
            }
            return cancel;
        }
    }
}
