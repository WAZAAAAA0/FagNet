using System.Collections.Generic;
using System.ComponentModel.Composition;
using FagNet.Core.Data;
using FagNet.Core.Plugin;

namespace RemoveStats
{
    [Export(typeof(GamePlugin))]
    public class RemoveStatsPlugin : GamePlugin
    {
        public RemoveStatsPlugin()
        {
            Name = "RemoveStats";
        }

        public override void OnBuyItem(Player plr, List<Item> itemsToBuy)
        {
            foreach (var item in itemsToBuy)
                item.EffectID = 0;
        }
    }
}
