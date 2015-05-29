using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using FagNet.Core.Constants;
using FagNet.Core.Constants.Packets;
using FagNet.Core.Data;
using FagNet.Core.Network;
using FagNet.Core.Plugin;

namespace CustomRoomRules
{
    [Export(typeof(GamePlugin))]
    public class CustomRoomRulesPlugin : GamePlugin
    {
        private static readonly Dictionary<string, List<Item>> _database = new Dictionary<string, List<Item>>()
        {
            {
                "HG", 
                new List<Item>()
                {
                    new Item() { Category = 2, SubCategory = 1, ItemID = 7 },
                    new Item() { Category = 2, SubCategory = 1, ItemID = 1111 },
                    new Item() { Category = 2, SubCategory = 1, ItemID = 2111 },
                }
            },
            {
                "Smash", 
                new List<Item>()
                {
                    new Item() { Category = 2, SubCategory = 1, ItemID = 6 },
                }
            },
            {
                "CS", 
                new List<Item>()
                {
                    new Item() { Category = 2, SubCategory = 0, ItemID = 2 },
                    new Item() { Category = 2, SubCategory = 0, ItemID = 1007 },
                    new Item() { Category = 2, SubCategory = 0, ItemID = 1008 },
                    new Item() { Category = 2, SubCategory = 0, ItemID = 1108 },
                    new Item() { Category = 2, SubCategory = 0, ItemID = 1109 },
                    new Item() { Category = 2, SubCategory = 0, ItemID = 1110 },
                    new Item() { Category = 2, SubCategory = 0, ItemID = 2108 },
                    new Item() { Category = 2, SubCategory = 0, ItemID = 2109 },
                    new Item() { Category = 2, SubCategory = 0, ItemID = 2110 },
                }
            },
        };

        public CustomRoomRulesPlugin()
        {
            Name = "CustomRoomRules";
        }

        public override bool OnBeginRound(Player plr, Room room)
        {
            var rule = CheckRule(room);
            if (rule == null) return false;

            if (CheckEquip(plr, rule)) return false;

            var ack = new Packet(EGamePacket.SResultAck);
            ack.Write((uint)EServerResult.WearingUnusbleItem);
            plr.Session.Send(ack);
            return true;
        }

        public override bool OnReadyRound(Player plr, Room room)
        {
            var rule = CheckRule(room);
            if (rule == null) return false;

            if (CheckEquip(plr, rule)) return false;

            var ack = new Packet(EGamePacket.SResultAck);
            ack.Write((uint)EServerResult.WearingUnusbleItem);
            plr.Session.Send(ack);
            return true;
        }

        private bool CheckEquip(Player plr, string rule)
        {
            if (!_database.ContainsKey(rule))
                return true;

            foreach (var itm in _database[rule])
            {
                var character = plr.Characters.FirstOrDefault(c => c.Slot == plr.ActiveCharSlot);
                if(character == null) continue;

                if ((from w in character.Weapons where w != 0 select plr.Inventory.First(e => e.ID == w)).Any(i => i.Category == itm.Category && i.SubCategory == itm.SubCategory && i.ItemID == itm.ItemID))
                    return false;
                if ((from w in character.Clothes where w != 0 select plr.Inventory.First(e => e.ID == w)).Any(i => i.Category == itm.Category && i.SubCategory == itm.SubCategory && i.ItemID == itm.ItemID))
                    return false;
                
                if(character.Skill == 0) continue;
                var skill = plr.Inventory.First(e => e.ID == character.Skill);
                if (skill.Category == itm.Category && skill.SubCategory == itm.SubCategory && skill.ItemID == itm.ItemID) return false;
            }

            return true;
        }

        private string CheckRule(Room room)
        {
            var name = room.Name;
            return (from entry in _database where name.Contains("CR: NO " + entry.Key) select entry.Key).FirstOrDefault();
        }
    }
}
