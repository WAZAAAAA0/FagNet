using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Linq;
using FagNet.Core.Constants.Packets;
using FagNet.Core.Data;
using FagNet.Core.Database;
using FagNet.Core.Network;
using FagNet.Core.Plugin;
using FagNet.Core.Utils;
using FagNetGame;

namespace GMCommands
{
    [Export(typeof(GamePlugin))]
    public class GMCommandsPlugin : GamePlugin
    {
        private readonly Dictionary<string, string> _commandDescriptions = new Dictionary<string, string>();
 
        public GMCommandsPlugin()
        {
            Name = "GMCommands";
            _commandDescriptions.Add("additem", HelperUtils.GetS4Color(255, 0, 0) + "/additem <category> <subCategory> <itemID> <productID> <playerID/Nickname>");
            _commandDescriptions.Add("closeroom", HelperUtils.GetS4Color(255, 0, 0) + "/closeroom");
            _commandDescriptions.Add("broadcast", HelperUtils.GetS4Color(255, 0, 0) + "/broadcast <message>");
            _commandDescriptions.Add("kick", HelperUtils.GetS4Color(255, 0, 0) + "/kick <playerID/Nickname>");
            _commandDescriptions.Add("roomkick", HelperUtils.GetS4Color(255, 0, 0) + "/roomkick <playerID/Nickname>");
            _commandDescriptions.Add("setlevel", HelperUtils.GetS4Color(255, 0, 0) + "/setlevel <level> <playerID/Nickname>");
        }

        public override string OnAdminAction(Player sender, string[] args)
        {
            switch (args[0])
            {
                case "additem":
                    return AddItem(args, sender);

                case "closeroom":
                    return CloseRoom(args, sender);

                case "broadcast":
                    return Broadcast(args, sender);

                case "kick":
                    return Kick(args, sender);

                case "roomkick":
                    return RoomKick(args, sender);

                case "setlevel":
                    return SetLevel(args, sender);
            }

            return "";
        }

        private string AddItem(string[] args, Player sender)
        {
            if (sender.GMLevel < 2)
                return HelperUtils.GetS4Color(255, 0, 0) + "Nope.";

            byte category;
            byte subCategory;
            ushort itemID;
            byte productID;
            ulong accountID;
            var nickname = "";

            if (args.Length < 6)
                return _commandDescriptions["additem"];

            if (!byte.TryParse(args[1], out category)) return _commandDescriptions["additem"];
            if (!byte.TryParse(args[2], out subCategory)) return _commandDescriptions["additem"];
            if (!ushort.TryParse(args[3], out itemID)) return _commandDescriptions["additem"];
            if (!byte.TryParse(args[4], out productID)) return _commandDescriptions["additem"];
            if (!ulong.TryParse(args[5], out accountID)) nickname = args[5];

            var time = DateTime.Now;
            var item = new Item
            {
                Category = category,
                SubCategory = subCategory,
                ItemID = itemID,
                ProductID = productID,
                PurchaseTime = HelperUtils.GetUnixTimestamp(time)
            };

            var shopItem = GameDatabase.Instance.GetShopItem(item.Category, item.SubCategory, item.ItemID, item.ProductID);
            if (shopItem == null)
            {
                item.ProductID = 1;
                item.Energy = 2400;
                item.ExpireTime = -1;
                //return HelperUtils.GetS4Color(255, 0, 0) + "ShopItem not found.";
            }
            else
            {
                item.Energy = shopItem.Energy;
                item.ExpireTime = (shopItem.Time == -1) ? -1 : HelperUtils.GetUnixTimestamp(time.AddSeconds(shopItem.Time));
            }

            var targetPlr = accountID > 0 ? GameServer.Instance.Players.GetPlayerByID(accountID) : GameServer.Instance.Players.GetPlayerByNickname(nickname);
            if (targetPlr == null)
                return HelperUtils.GetS4Color(255, 0, 0) + "Player not found.";

            var id = GameDatabase.Instance.CreateItem(item, targetPlr.AccountID);
            if (id == 0)
                return HelperUtils.GetS4Color(255, 0, 0) + "Failed to create item.";
            item.ID = id;
            item.SetupAPWeapon();
            targetPlr.AddItem(item);

            return HelperUtils.GetS4Color(0, 255, 0) + "Done.";
        }

        private string CloseRoom(string[] args, Player sender)
        {
            if (sender.GMLevel < 2)
                return HelperUtils.GetS4Color(255, 0, 0) + "Nope.";

            if (sender.Room == null)
                return HelperUtils.GetS4Color(255, 0, 0) + "You're not in a room.";

            var ls = sender.Room.Players.Values.ToList();
            ls.Reverse();
            foreach (var plr in ls)
                sender.Room.Leave(plr, 1);
            return HelperUtils.GetS4Color(0, 255, 0) + "Done.";
        }

        private string Broadcast(string[] args, Player sender)
        {
            if (sender.GMLevel < 2)
                return HelperUtils.GetS4Color(255, 0, 0) + "Nope.";

            if (args.Length < 2)
                return _commandDescriptions["broadcast"];

            GameServer.Instance.BroadcastNotice(args[1]);
            return HelperUtils.GetS4Color(0, 255, 0) + "Done.";
        }

        private string Kick(string[] args, Player sender)
        {
            if (sender.GMLevel < 2)
                return HelperUtils.GetS4Color(255, 0, 0) + "Nope.";

            ulong accountID;
            var nickname = "";

            if (args.Length < 2)
                return _commandDescriptions["kick"];

            if (!ulong.TryParse(args[1], out accountID)) nickname = args[1];
            var targetPlr = accountID > 0 ? GameServer.Instance.Players.GetPlayerByID(accountID) : GameServer.Instance.Players.GetPlayerByNickname(nickname);
            if (targetPlr == null)
                return HelperUtils.GetS4Color(255, 0, 0) + "Player not found.";
            targetPlr.Session.StopListening();

            return HelperUtils.GetS4Color(0, 255, 0) + "Done.";
        }

        private string RoomKick(string[] args, Player sender)
        {
            if (sender.GMLevel < 2)
                return HelperUtils.GetS4Color(255, 0, 0) + "Nope.";

            ulong accountID;
            var nickname = "";

            if (args.Length < 2)
                return _commandDescriptions["roomkick"];

            if (!ulong.TryParse(args[1], out accountID)) nickname = args[1];
            var targetPlr = accountID > 0 ? GameServer.Instance.Players.GetPlayerByID(accountID) : GameServer.Instance.Players.GetPlayerByNickname(nickname);
            if (targetPlr == null)
                return HelperUtils.GetS4Color(255, 0, 0) + "Player not found.";
            if (targetPlr.Room == null)
                return HelperUtils.GetS4Color(255, 0, 0) + "Player is not in a room.";
            targetPlr.Room.Leave(targetPlr, 1);

            return HelperUtils.GetS4Color(0, 255, 0) + "Done.";
        }

        private string SetLevel(string[] args, Player sender)
        {
            if (sender.GMLevel < 2)
                return HelperUtils.GetS4Color(255, 0, 0) + "Nope.";

            byte level;
            ulong accountID;
            var nickname = "";

            if (args.Length < 3)
                return _commandDescriptions["setlevel"];

            if (!byte.TryParse(args[1], out level)) return _commandDescriptions["setlevel"];
            if (!ulong.TryParse(args[2], out accountID)) nickname = args[2];
            var targetPlr = accountID > 0 ? GameServer.Instance.Players.GetPlayerByID(accountID) : GameServer.Instance.Players.GetPlayerByNickname(nickname);
            if (targetPlr == null)
                return HelperUtils.GetS4Color(255, 0, 0) + "Player not found.";

            targetPlr.Level = level;
            GameDatabase.Instance.UpdateEXPLevel(targetPlr);

            var msg = string.Format("{0} has set your level to {1}", sender.Nickname, level);
            var len = (ushort) (msg.Length + 1);
            var ack = new Packet(EGamePacket.SNoticeAck);
            ack.Write(len);
            ack.Write(msg);
            targetPlr.Session.Send(ack);

            return HelperUtils.GetS4Color(0, 255, 0) + "Done.";
        }
    }
}
