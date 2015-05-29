using System;
using System.Collections.Generic;
using FagNet.Core.Constants;
using FagNet.Core.Utils;
using FagNet.Core.Data;

namespace FagNet.Core.Database
{
    public class GameDatabase : Database
    {
        public static GameDatabase Instance
        { get { return Singleton<GameDatabase>.Instance; } }

        public ChannelCollection GetChannels(EServerType serverType)
        {
            var col = new ChannelCollection();
            using (var con = GetConnection())
            {
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM channels";
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var channel = new Channel
                            {
                                ServerType = serverType,
                                ID = r.GetUInt16("ID"),
                                Name = r.GetString("Name")
                            };
                            col.TryAdd(channel.ID, channel);
                        }
                    }
                }
            }
            return col;
        }

        public Player GetPlayer(ulong accID)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "SELECT * FROM accounts WHERE ID=@ID", "@ID", accID))
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            return null;

                        var player = new Player
                        {
                            AccountID = (ulong) r.GetInt64("ID"),
                            Nickname = r.GetString("Nickname"),
                            EXP = (uint) r.GetInt32("EXP"),
                            Level = (uint) r.GetInt32("Level"),
                            TutorialCompleted = (r.GetByte("TutorialCompleted") > 0),
                            ActiveCharSlot = r.GetByte("ActiveCharSlot"),
                            PEN = (uint) r.GetInt32("PEN"),
                            AP = (uint) r.GetInt32("AP"),
                            GMLevel = r.GetByte("GMLevel"),
                            Licenses = GetLicenses(accID),
                            Inventory = GetInventory(accID),
                            Characters = GetCharacters(accID),
                            TDStats = GetTDStats(accID),
                            DMStats = GetDMStats(accID),
                            DenyList = GetDenyList(accID),
                            FriendList = GetFriendList(accID)
                        };
                        return player;
                    }
                }
            }
        }

        public void CreatePlayer(Player plr)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "INSERT INTO accounts(ID, Nickname, Level, PEN, AP, GMLevel) VALUES(@ID, @Nickname, @Level, @PEN, @AP, @GMLevel); INSERT INTO account_tdstats(ID) VALUES(@ID); INSERT INTO account_dmstats(ID) VALUES(@ID);", 
                    "@ID", plr.AccountID, "@Nickname", plr.Nickname, "@Level", plr.Level, "@PEN", plr.PEN, "@AP", plr.AP, "@GMLevel", plr.GMLevel))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public TDStatistics GetTDStats(ulong accID)
        {
            var stats = new TDStatistics();
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "SELECT * FROM account_tdstats WHERE ID=@ID", "@ID", accID))
                {
                    using(var r = cmd.ExecuteReader())
                    {
                        if(!r.Read())
                            return stats;

                        stats.TotalTouchdowns = r.GetUInt32("TDs");
                        stats.TotalTouchdownAssists = r.GetUInt32("TDAssists");
                        stats.TotalKills = r.GetUInt32("Kills");
                        stats.TotalKillAssists = r.GetUInt32("KillAssists");
                        stats.TotalOffense = r.GetUInt32("Offense");
                        stats.TotalOffenseAssists = r.GetUInt32("OffenseAssists");
                        stats.TotalDefense = r.GetUInt32("Defense");
                        stats.TotalDefenseAssists = r.GetUInt32("DefenseAssists");
                        stats.TotalRecovery = r.GetUInt32("Recovery");
                        stats.TotalMatches = r.GetUInt32("Matches");
                        stats.Won = r.GetUInt32("Won");
                        stats.Lost = r.GetUInt32("Lost");
                    }
                }
            }
            return stats;
        }

        public DMStatistics GetDMStats(ulong accID)
        {
            var stats = new DMStatistics();
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "SELECT * FROM account_dmstats WHERE ID=@ID", "@ID", accID))
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            return stats;

                        stats.TotalKills = r.GetUInt32("Kills");
                        stats.TotalKillAssists = r.GetUInt32("KillAssists");
                        stats.TotalDeaths = r.GetUInt32("Deaths");
                        stats.TotalMatches = r.GetUInt32("Matches");
                        stats.Won = r.GetUInt32("Won");
                        stats.Lost = r.GetUInt32("Lost");
                    }
                }
            }
            return stats;
        }

        public List<Friend> GetFriendList(ulong accID)
        {
            var ls = new List<Friend>();

            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "SELECT * FROM account_friend WHERE AccountID=@AID", "@AID", accID))
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            ls.Add(new Friend() { ID = r.GetUInt64("FriendID"), Nickname = r.GetString("FriendName"), Accepted = r.GetBoolean("Accepted") });
                    }
                }
            }

            return ls;
        }

        public void AddFriend(ulong accID, ulong friendID, string friendName, bool accepted = false)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "INSERT INTO account_friend(AccountID, FriendID, FriendName, Accepted) VALUES(@AccountID, @IID, @IName, @Accepted)",
                    "@AccountID", accID, "@IID", friendID, "@IName", friendName, "@Accepted", accepted))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void RemoveFriend(ulong accID, ulong friendID)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "DELETE FROM account_friend WHERE AccountID=@AID AND FriendID=@IID",
                    "@AID", accID, "@IID", friendID))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateFriend(ulong accID, ulong friendID, bool accepted)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "UPDATE account_friend SET Accepted=@Accepted WHERE AccountID=@AID AND FriendID=@IID",
                    "@Accepted", accepted, "@AID", accID, "@IID", friendID))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public Dictionary<ulong, string> GetDenyList(ulong accID)
        {
            var ls = new Dictionary<ulong, string>();

            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "SELECT * FROM account_deny WHERE AccountID=@AID", "@AID", accID))
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            ls.Add(r.GetUInt64("IgnoreID"), r.GetString("IgnoreName"));
                    }
                }
            }

            return ls;
        }

        public void AddDeny(ulong accID, ulong ignoreID, string ignoreName)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "INSERT INTO account_deny(AccountID, IgnoreID, IgnoreName) VALUES(@AccountID, @IID, @IName)", 
                    "@AccountID", accID, "@IID", ignoreID, "@IName", ignoreName))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void RemoveDeny(ulong accID, ulong ignoreID)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "DELETE FROM account_deny WHERE AccountID=@AID AND IgnoreID=@IID",
                    "@AID", accID, "@IID", ignoreID))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateTDStats(ulong accID, TDStatistics stats)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con,
                    "UPDATE account_tdstats SET TDs=@TD, TDAssists=@TDA, Kills=@Kills, KillAssists=@KillA, Offense=@Offense, OffenseAssists=@OffenseA, Defense=@Defense, DefenseAssists=@DefenseA, Recovery=@Recovery, Matches=@Matches, Won=@Won, Lost=@Lost WHERE ID=@ID",
                    "@TD", stats.TotalTouchdowns, "@TDA", stats.TotalTouchdownAssists, "@Kills", stats.TotalKills, "@KillA",
                    stats.TotalKillAssists, "@Offense", stats.TotalOffense, "@OffenseA",
                    stats.TotalOffenseAssists, "@Defense", stats.TotalDefense, "@DefenseA",
                    stats.TotalDefenseAssists, "@Recovery", stats.TotalRecovery, "@Matches", stats.TotalMatches, "@Won", stats.Won, "@Lost", stats.Lost, "@ID", accID))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateDMStats(ulong accID, DMStatistics stats)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con,
                    "UPDATE account_dmstats SET Kills=@Kills, KillAssists=@KillA, Deaths=@Deaths, Matches=@Matches, Won=@Won, Lost=@Lost WHERE ID=@ID",
                    "@Kills", stats.TotalKills, "@KillA", stats.TotalKillAssists, 
                    "@Deaths", stats.TotalDeaths, "@Matches", stats.TotalMatches, "@Won", stats.Won, "@Lost", stats.Lost, "@ID", accID))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateMoney(Player plr)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con,
                    "UPDATE accounts SET PEN=@PEN, AP=@AP WHERE ID=@ID",
                    "@PEN", plr.PEN, "@AP", plr.AP, "@ID", plr.AccountID))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateTutorialFlag(Player plr)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con,
                    "UPDATE accounts SET TutorialCompleted=@TC WHERE ID=@ID",
                    "@TC", plr.TutorialCompleted, "@ID", plr.AccountID))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<Item> GetInventory(ulong accID)
        {
            var ls = new List<Item>();
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "SELECT * FROM account_inventory WHERE AccountID=@AccountID", "@AccountID", accID))
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var item = new Item
                            {
                                ID = (uint) r.GetInt32("ID"),
                                Category = r.GetByte("Category"),
                                SubCategory = r.GetByte("SubCategory"),
                                ItemID = (ushort) r.GetInt16("ItemID"),
                                ProductID = r.GetByte("ProductID"),
                                EffectID = (uint) r.GetInt32("EffectID"),
                                PurchaseTime = r.GetInt64("PurchaseTime"),
                                ExpireTime = r.GetInt64("ExpireTime"),
                                TimeUsed = r.GetInt32("TimeUsed"),
                                Energy = r.GetInt32("Energy")
                            };
                            item.SetupAPWeapon();

                            ls.Add(item);
                        }
                    }
                }
            }
            return ls;
        }

        public List<byte> GetLicenses(ulong accID)
        {
            var ls = new List<byte>();
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "SELECT * FROM account_license WHERE AccountID=@AccountID", "@AccountID", accID))
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            ls.Add(r.GetByte("LicenseID"));
                    }
                }
            }
            return ls;
        }

        public List<Character> GetCharacters(ulong accID)
        {
            var ls = new List<Character>();
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "SELECT * FROM account_characters WHERE AccountID=@AccountID", "@AccountID", accID))
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var character = new Character();
                            character.Slot = r.GetByte("Slot");
                            character.Avatar = r.GetUInt32("Avatar");
                            character.Weapons = new ulong[3];
                            character.Weapons[0] = r.GetUInt64("Weapon1");
                            character.Weapons[1] = r.GetUInt64("Weapon2");
                            character.Weapons[2] = r.GetUInt64("Weapon3");
                            character.Skill = r.GetUInt64("Skill");
                            character.Clothes = new ulong[7];
                            character.Clothes[0] = r.GetUInt64("Hair");
                            character.Clothes[1] = r.GetUInt64("Face");
                            character.Clothes[2] = r.GetUInt64("Shirt");
                            character.Clothes[3] = r.GetUInt64("Pants");
                            character.Clothes[4] = r.GetUInt64("Gloves");
                            character.Clothes[5] = r.GetUInt64("Shoes");
                            character.Clothes[6] = r.GetUInt64("Special");
                            ls.Add(character);
                        }
                    }
                }
            }
            return ls;
        }

        public void CreateCharacter(ulong accID, uint slot, uint avatar)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "INSERT INTO account_characters(AccountID, Slot, Avatar) VALUES(@AccountID, @Slot, @Avatar)", "@AccountID", accID, "@Slot", slot, "@Avatar", avatar))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteCharacter(ulong accID, uint slot)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "DELETE FROM account_characters WHERE AccountID=@AID AND Slot=@Slot", "@AID", accID, "@Slot", slot))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void SetActiveCharSlot(ulong accID, byte slot)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "UPDATE accounts SET ActiveCharSlot=@ActiveCharSlot WHERE ID=@ID", "@ActiveCharSlot", slot, "@ID", accID))
                    cmd.ExecuteNonQuery();
            }
        }

        public ulong CreateItem(Item item, ulong accID)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con,
                    "INSERT INTO account_inventory(AccountID, Category, SubCategory, ItemID, ProductID, EffectID, PurchaseTime, ExpireTime, Energy) VALUES(@AccountID, @Category, @SubCategory, @ItemID, @ProductID, @EffectID, @PurchaseTime, @ExpireTime, @Energy); SHOW TABLE STATUS LIKE 'account_inventory';",
                    "@AccountID", accID, "@Category", item.Category, "@SubCategory", item.SubCategory, "@ItemID", item.ItemID, "@ProductID", item.ProductID, "@EffectID", item.EffectID, "@PurchaseTime", item.PurchaseTime, "@ExpireTime", item.ExpireTime, "@Energy", item.Energy))
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            return 0;
                        return (ulong)(r.GetInt64("Auto_increment") - 1);
                    }
                }
            }
        }

        public void RemoveItem(ulong id)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "DELETE FROM account_inventory WHERE ID=@ID", "@ID", id))
                    cmd.ExecuteNonQuery();
            }
        }

        public void UpdateItem(Item item)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "UPDATE account_inventory SET TimeUsed=@TimeUsed, Energy=@Energy WHERE ID=@ID;", "@ID", item.ID, "@TimeUsed", item.TimeUsed, "@Energy", item.Energy))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public ShopItem GetShopItem(byte category, byte subCategory, ushort itemID, byte productID)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "SELECT * FROM shop WHERE Category=@Category AND SubCategory=@SubCategory AND ItemID=@ItemID AND ProductID=@ProductID", "@Category", category, "@SubCategory", subCategory, "@ItemID", itemID, "@ProductID", productID))
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            return null;

                        var itm = new ShopItem
                        {
                            Type = r.GetByte("Type"),
                            Category = category,
                            SubCategory = subCategory,
                            ItemID = itemID,
                            ProductID = productID,
                            Price = r.GetUInt32("Price"),
                            Cash = r.GetUInt32("Cash"),
                            Energy = r.GetInt32("Energy"),
                            Time = r.GetInt32("Time")
                        };
                        return itm;
                    }
                }
            }
        }

        public void AddLicense(ulong accID, byte licenseID)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "INSERT INTO account_license(AccountID, LicenseID) VALUES(@AccountID, @LicenseID)", "@AccountID", accID, "@LicenseID", licenseID))
                    cmd.ExecuteNonQuery();
            }
        }

        public byte GetGMLevel(ulong id)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "SELECT GMLevel FROM accounts WHERE ID=@ID", "@ID", id))
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            return 0;

                        return r.GetByte("GMLevel");
                    }
                }
            }
        }

        public void UpdateCharacterEquip(ulong accID, Character character)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con,
                    "UPDATE account_characters SET Weapon1=@W1, Weapon2=@W2, Weapon3=@W3, Skill=@Skill, Hair=@Hair, Face=@Face, Shirt=@Shirt, Pants=@Pants, Gloves=@Gloves, Shoes=@Shoes, Special=@Special WHERE AccountID=@ID AND Slot=@CSlot",
                    "@W1", character.Weapons[0], "@W2", character.Weapons[1], "@W3", character.Weapons[2], "@Skill", character.Skill,
                    "@Hair", character.Clothes[0], "@Face", character.Clothes[1], "@Shirt", character.Clothes[2], "@Pants", character.Clothes[3], "@Gloves", character.Clothes[4], "@Shoes", character.Clothes[5], "@Special", character.Clothes[6],
                    "@ID", accID, "@CSlot", character.Slot))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateEXPLevel(Player plr)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con,
                    "UPDATE accounts SET EXP=@EXP, Level=@Level WHERE ID=@ID",
                    "@EXP", plr.EXP, "@Level", plr.Level, "@ID", plr.AccountID))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateOnlineFlag(ulong accID, bool isOn)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con,
                    "UPDATE accounts SET IsOnline=@ON WHERE ID=@ID",
                    "@ON", isOn, "@ID", accID))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateOnlineFlags()
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "UPDATE accounts SET IsOnline=0"))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public bool IsValidMapID(byte id)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con,
                    "SELECT * FROM maps WHERE MapID=@ID",
                    "@ID", id))
                {
                    using (var r = cmd.ExecuteReader())
                        return r.Read();
                }
            }
        }
    }
}
