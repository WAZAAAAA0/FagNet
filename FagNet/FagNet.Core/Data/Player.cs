using System;
using System.Collections.Generic;
using System.Linq;
using FagNet.Core.Constants.Packets;
using FagNet.Core.Network;
using FagNet.Core.Database;
using FagNet.Core.Constants;

namespace FagNet.Core.Data
{
    #region Score stuff
    public class GameScore
    {
        public DateTime JoinTime { get; set; }
        public uint TotalPoints { get; set; }

        public virtual uint CalculateEXP()
        {
            return 0;
        }
    }

    public class TDStatistics
    {
        public uint TotalTouchdowns { get; set; }
        public uint TotalTouchdownAssists { get; set; }
        public uint TotalKills { get; set; }
        public uint TotalKillAssists { get; set; }
        public uint TotalOffense { get; set; }
        public uint TotalOffenseAssists { get; set; }
        public uint TotalDefense { get; set; }
        public uint TotalDefenseAssists { get; set; }
        public uint TotalRecovery { get; set; }
        public uint TotalMatches { get; set; }

        public uint Won { get; set; }
        public uint Lost { get; set; }

        public uint CalculateTotalScore()
        {
            var totalScore = TotalTouchdowns * 10;
            totalScore += TotalTouchdownAssists * 5;
            totalScore += TotalKills * 2;
            totalScore += TotalKillAssists;
            totalScore += TotalOffense * 4;
            totalScore += TotalOffenseAssists * 2;
            totalScore += TotalDefense * 4;
            totalScore += TotalDefenseAssists * 2;
            totalScore += TotalRecovery * 2;
            return totalScore;
        }

        public float CalculateOffensePerMatch()
        {
            if (TotalMatches == 0)
                return 0.0f;
            var points = TotalOffense * 4;
            points += TotalOffenseAssists * 2;

            return points / (float)TotalMatches;
        }

        public float CalculateDefensePerMatch()
        {
            if (TotalMatches == 0)
                return 0.0f;
            var points = TotalDefense * 4;
            points += TotalDefenseAssists * 2;

            return points / (float)TotalMatches;
        }

        public float CalculateRecoveryPerMatch()
        {
            if (TotalMatches == 0)
                return 0.0f;
            var points = TotalRecovery * 2;
            return points / (float)TotalMatches;
        }

        public float CalculateWinRate()
        {
            if (TotalMatches == 0)
                return 0.0f;
            return (Won / (float)TotalMatches * 100.0f);
        }
    }

    public class DMStatistics
    {
        public uint TotalKills { get; set; }
        public uint TotalKillAssists { get; set; }
        public uint TotalDeaths { get; set; }
        public uint TotalMatches { get; set; }

        public uint Won { get; set; }
        public uint Lost { get; set; }
        
        public uint CalculateTotalScore()
        {
            var totalScore = TotalKills * 2;
            totalScore += TotalKillAssists;
            return totalScore;
        }

        public float CalculateWinRate()
        {
            if (TotalMatches == 0)
                return 0.0f;
            return (Won / (float)TotalMatches * 100.0f);
        }
    }

    public class TDGameScore : GameScore
    {
        public uint TDScore { get; set; }
        public uint TDAssists { get; set; }

        public uint Kills { get; set; }
        public uint KillAssists { get; set; }
        public uint Offense { get; set; }
        public uint OffenseAssists { get; set; }
        public uint Defense { get; set; }
        public uint DefenseAssists { get; set; }
        public uint Recovery { get; set; }

        public override uint CalculateEXP()
        {
            var ts = DateTime.Now - JoinTime;
            if (ts.TotalSeconds < 0 || TotalPoints == 0)
                return 0;
            return (uint)ts.TotalSeconds / 4 + (TDScore * 15) + (100 * TotalPoints / (500 + 2 * TotalPoints) * 14);
        }
    }

    public class DMGameScore : GameScore
    {
        public uint Kills { get; set; }
        public uint KillAssists { get; set; }
        public uint Deaths { get; set; }

        public override uint CalculateEXP()
        {
            //var ts = DateTime.Now - JoinTime;

            return 200;
        }
    }

    public class SurvivalGameScore : GameScore
    {
        public uint Kills { get; set; }

        public override uint CalculateEXP()
        {
            //var ts = DateTime.Now - JoinTime;

            return 200;
        }
    }
    #endregion

    #region Community stuff

    public class Friend
    {
        public ulong ID { get; set; }
        public string Nickname { get; set; }
        public bool Accepted { get; set; }
    }
    #endregion

    public class Player
    {
        #region LevelSystem
        public static Dictionary<int, int> LevelSystem = new Dictionary<int, int>
        { 
            { 0, 1400 },
            { 1, 1600 },
            { 2, 1800 },
            { 3, 2000 },
            { 4, 2200 },
            { 5, 2400 },
            { 6, 2800 },
            { 7, 3200 },
            { 8, 3600 },
            { 9, 4000 },
            { 10, 4400 },
            { 11, 5400 },
            { 12, 6400 },
            { 13, 7400 },
            { 14, 8400 },
            { 15, 9400 },
            { 16, 11000 },
            { 17, 13000 },
            { 18, 15000 },
            { 19, 17000 },
            { 20, 19000 },
            { 21, 23000 },
            { 22, 27000 },
            { 23, 31000 },
            { 24, 35000 },
            { 25, 39000 },
            { 26, 45000 },
            { 27, 50000 },
            { 28, 55000 },
            { 29, 60000 },
            { 30, 65000 },
            { 31, 73820 },
            { 32, 82640 },
            { 33, 91460 },
            { 34, 100280 },
            { 35, 109100 },
            { 36, 120620 },
            { 37, 132140 },
            { 38, 143660 },
            { 39, 155180 },
            { 40, 166700 },
            { 41, 181280 },
            { 42, 195860 },
            { 43, 210440 },
            { 44, 225020 },
            { 45, 239600 },
            { 46, 257600 },
            { 47, 275600 },
            { 48, 293600 },
            { 49, 311600 },
            { 50, 329600 },
            { 51, 351380 },
            { 52, 373160 },
            { 53, 394940 },
            { 54, 416720 },
            { 55, 438500 },
            { 56, 464420 },
            { 57, 490340 },
            { 58, 516260 },
            { 59, 542180 },
            { 60, 568100 },
            { 61, 598520 },
            { 62, 628940 },
            { 63, 659360 },
            { 64, 689780 },
            { 65, 720200 },
            { 66, 755480 },
            { 67, 790760 },
            { 68, 826040 },
            { 69, 861320 },
            { 70, 896600 },
            { 71, 937100 },
            { 72, 977600 },
            { 73, 1018100 },
            { 74, 1058600 },
            { 75, 1099100 },
            { 76, 1145180 },
            { 77, 1191260 },
            { 78, 1237340 },
            { 79, 1283420 },
            { 80, 1329500 },
            { 81, 1381520 },
            { 82, 1433540 },
            { 83, 1485560 },
            { 84, 1537580 },
            { 85, 1589600 },
            { 86, 1647920 },
            { 87, 1706240 },
            { 88, 1764560 },
            { 89, 1822880 },
            { 90, 1881200 },
            { 91, 1946180 },
            { 92, 2011160 },
            { 93, 2076140 },
            { 94, 2141120 },
            { 95, 2206100 },
            { 96, 2278100 },
            { 97, 2350100 },
            { 98, 2422100 },
            { 99, 2494100 },
            { 100, 0 }, // 100 is max level
        };
#endregion

        public UInt64 AccountID { get; set; }
        public uint SessionID { get; set; }
        public TcpSession Session { get; set; }
        public string Username { get; set; }
        public string Nickname { get; set; }
        public byte GMLevel { get; set; }

        public List<Character> Characters { get; set; }
        public List<byte> Licenses { get; set; }
        public List<Item> Inventory { get; set; }

        public ushort ServerID { get; set; }
        public Channel Channel { get; set; }
        public Room Room { get; set; }

        public bool TutorialCompleted { get; set; }
        public uint AP { get; set; }
        public uint PEN { get; set; }
        public uint EXP { get; set; }
        public uint Level { get; set; }
        public byte ActiveCharSlot { get; set; }

        public uint LastSyncTime { get; set; }
        public uint Ping { get; set; }

        // Score stuff
        public TDStatistics TDStats { get; set; }
        public DMStatistics DMStats { get; set; }

        // Community
        public EAllowCommunityRequest AllowCombiRequest { get; set; }
        public EAllowCommunityRequest AllowInvite { get; set; }
        public EAllowCommunityRequest AllowInfoRequest { get; set; }
        public EAllowCommunityRequest AllowFriendRequest { get; set; }
        public Dictionary<ulong, string> DenyList { get; set; }
        public List<Friend> FriendList { get; set; }

        public byte CommunityByte { get; set; }
        public byte[] CommunityData { get; set; }

        // UDP
        public uint PublicIP { get; set; }
        public ushort PublicPort { get; set; }
        public uint PrivateIP { get; set; }
        public ushort PrivatePort { get; set; }
        public ushort NATUnk { get; set; }
        public byte ConnectionType { get; set; }

        // Relay
        public List<byte> RelayProxies { get; set; }

        // Room stuff
        public byte SlotID { get; set; }
        public ETeam Team { get; set; }
        public bool IsReady { get; set; }
        public EPlayerState State { get; set; }
        public GameScore GameScore { get; set; }
        public EPlayerGameMode GameMode { get; set; }
        public bool IsSpawned { get; set; }

        public Player()
        {
            Licenses = new List<byte>();
            Inventory = new List<Item>();
            Characters = new List<Character>();
            DenyList = new Dictionary<ulong, string>();
            FriendList = new List<Friend>();
            TDStats = new TDStatistics();
            ConnectionType = 4;
            IsSpawned = false;
            RelayProxies = new List<byte>();
            GameMode = EPlayerGameMode.Normal;
            GameScore = new GameScore();
        }

        public void ResetScore()
        {
            if (Room == null)
            {
                GameScore = new GameScore();
                return;
            }
            switch (Room.GameRule)
            {
                case EGameRule.Touchdown:
                    GameScore = new TDGameScore();
                    break;
                case EGameRule.Deathmatch:
                    GameScore = new DMGameScore();
                    break;
                case EGameRule.Survival:
                    GameScore = new SurvivalGameScore();
                    break;
            }
        }

        public int CalculateTotalEXP()
        {
            var totalEXP = (int)EXP;
            for (var i = 0; i < Level; i++)
                totalEXP += LevelSystem[i];
            return totalEXP;
        }

        public void SetNewTDStats(bool win = false)
        {
            var score = (TDGameScore) GameScore;
            TDStats.TotalMatches++;
            if (win)
                TDStats.Won++;
            else
                TDStats.Lost++;

            TDStats.TotalKills += score.Kills;
            TDStats.TotalKillAssists += score.KillAssists;
            TDStats.TotalOffense += score.Offense;
            TDStats.TotalOffenseAssists += score.OffenseAssists;
            TDStats.TotalDefense += score.Defense;
            TDStats.TotalDefenseAssists += score.DefenseAssists;
            TDStats.TotalRecovery += score.Recovery;
            TDStats.TotalTouchdowns += score.TDScore;
            TDStats.TotalTouchdownAssists += score.TDAssists;
            GameDatabase.Instance.UpdateTDStats(AccountID, TDStats);
        }

        public void SetNewDMStats(bool win = false)
        {
            var score = (DMGameScore) GameScore;
            DMStats.TotalMatches++;
            if (win)
                DMStats.Won++;
            else
                DMStats.Lost++;

            DMStats.TotalKills += score.Kills;
            DMStats.TotalKillAssists += score.KillAssists;
            DMStats.TotalDeaths += score.Deaths;
            GameDatabase.Instance.UpdateDMStats(AccountID, DMStats);
        }

        public void UpdateEuqip()
        {
            var timePlayed = DateTime.Now - GameScore.JoinTime;

            var character = Characters.First(e => e.Slot == ActiveCharSlot);
            if (character == null)
            {
                Session.StopListening();
                return;
            }
            foreach (var weapon in character.Weapons.Where(w => w != 0))
            {
                var res = Inventory.Where(e => e.ID == weapon);
                var items = res as IList<Item> ?? res.ToList();

                if (!items.Any())
                {
                    Session.StopListening();
                    return;
                }

                var itm = items.First();

                itm.TimeUsed += (int)timePlayed.TotalSeconds;
                if (itm.Energy != -1)
                {
                    itm.Energy -= (int)(((float)timePlayed.TotalSeconds / 18000f) * itm.Energy);
                    if (itm.Energy < 0)
                        itm.Energy = 0;
                }
                GameDatabase.Instance.UpdateItem(itm);
            }
            foreach (var clothes in character.Clothes.Where(c => c != 0))
            {
                var res = Inventory.Where(e => e.ID == clothes);
                var items = res as IList<Item> ?? res.ToList();

                if (!items.Any())
                {
                    Session.StopListening();
                    return;
                }

                var itm = items.First();

                itm.TimeUsed += (int)timePlayed.TotalSeconds;
                if (itm.Energy != -1)
                {
                    itm.Energy -= (int)(((float)timePlayed.TotalSeconds / 18000f) * itm.Energy);
                    if (itm.Energy < 0)
                        itm.Energy = 0;
                }
                GameDatabase.Instance.UpdateItem(itm);
            }
            if (character.Skill == 0)
                return;
            var res2 = from e in Inventory
                      where e.ID == character.Skill
                      select e;
            var skill = res2 as IList<Item> ?? res2.ToList();
            if (!skill.Any())
            {
                Session.StopListening();
                return;
            }

            var itm2 = skill.First();

            itm2.TimeUsed += (int)timePlayed.TotalSeconds;
            if (itm2.Energy != -1)
            {
                itm2.Energy -= (int)(((float)timePlayed.TotalSeconds / 18000f) * itm2.Energy);
                if (itm2.Energy < 0)
                    itm2.Energy = 0;
            }
            GameDatabase.Instance.UpdateItem(itm2);
        }

        public void UpdateMoney()
        {
            GameDatabase.Instance.UpdateMoney(this);
            var ack = new Packet(EGamePacket.SCashUpdateAck);
            ack.Write(PEN);
            ack.Write(AP);
            Session.Send(ack);
        }

        public void AddItem(Item item)
        {
            Inventory.Add(item);

            var ack = new Packet(EGamePacket.SInventoryAddItemAck);
            ack.Write(item.ID);
            ack.Write(item.Category);
            ack.Write(item.SubCategory);
            ack.Write(item.ItemID);
            ack.Write(item.ProductID);
            ack.Write(item.EffectID);
            ack.Write(item.SellPrice);
            ack.Write(item.PurchaseTime);
            ack.Write(item.ExpireTime);
            ack.Write(item.Energy);
            ack.Write(item.TimeLeft);
            Session.Send(ack);
        }
    }
}
