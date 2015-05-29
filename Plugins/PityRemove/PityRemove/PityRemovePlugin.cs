using System.ComponentModel.Composition;
using FagNet.Core.Data;
using FagNet.Core.Plugin;

namespace PityRemove
{
    [Export(typeof(GamePlugin))]
    public class PityRemovePlugin : GamePlugin
    {
        public PityRemovePlugin()
        {
            Name = "PityRemove";
        }
        public override bool OnCreateRoom(Player plr, Room room)
        {
            room.IsBalanced = false;
            return false;
        }
    }
}
