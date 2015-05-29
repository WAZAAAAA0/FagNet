using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FagNet.Core.Constants.Packets;
using FagNet.Core.Database;
using FagNet.Core.Network;
using FagNet.Core.Constants;

namespace FagNet.Core.Data
{
    public class Room
    {
        #region Properties
        public uint ID { get; set; }
        public uint TunnelID { get; set; }
        public Channel Channel { get; set; }

        public EServerType ServerType { get; set; }

        public byte[] MatchKey { get; set; }
        public string Name { get; set; }
        public uint Password { get; set; }
        public int Unk { get; set; }
        public uint TimeLimit { get; set; }
        public uint ScoreLimit { get; set; }
        public bool IsFriendly { get; set; }
        public bool IsBalanced { get; set; }
        public byte MinLevel { get; set; }
        public byte MaxLevel { get; set; }
        public byte EquipLimit { get; set; }
        public bool IsNoIntrusion { get; set; }
        public EGameRuleState State { get; set; }
        public EGameTimeState TimeState { get; set; }
        public ulong MasterID { get; set; }
        public bool TDWaiting { get; set; }
        public uint Ping { get; set; }

        /*
         * 
         *  gameType = matchKey[0] & 1;
            gameRule = matchKey[0] >> 4;
            publicType = (matchKey[0] >> 1) & 1;
            joinAuth = (matchKey[0] >> 2) & 1;
            mapID = matchKey[1];
            playerCountLimit = matchKey[2];
            gmRoom = (matchKey[3] >> 3) & 1;
         * 
         * */
        public byte GameType
        {
            get { return (byte)(MatchKey[0] & 1); }
        }
        public byte PublicType
        {
            get { return (byte)((MatchKey[0] >> 1) & 1); }
        }
        public byte JoinAuth
        {
            get { return (byte)((MatchKey[0] >> 2) & 1); }
        }
        public byte IsObserveEnabled
        {
            get { return (byte)((MatchKey[3] >> 1) & 1); }
        }
        public EGameRule GameRule
        {
            get { return (EGameRule)(byte)(MatchKey[0] >> 4); }
            set
            {
                MatchKey[0] = (byte)(GameType | PublicType << 1 | JoinAuth << 2 | (byte)value << 4);
            }
        }
        public byte MapID
        {
            get { return MatchKey[1]; }
            set { MatchKey[1] = value; }
        }
        public int PlayerLimit
        {
            get
            {
                switch (MatchKey[2])
                {
                    case 8:
                        return 12;
                    case 7:
                        return 10;
                    case 6:
                        return 8;
                    case 5:
                        return 6;
                    case 3:
                        return 4;
                }

                return 0;
            }
        }
        public int SpectatorLimit
        {
            get { return 12 - PlayerLimit; }
        }

        public uint ScoreAlpha { get; set; }
        public uint ScoreBeta { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime RoundStartTime { get; set; }
        public DateTime CreationTime { get; set; }

        private readonly PlayerCollection _players = new PlayerCollection();
        public PlayerCollection Players { get { return _players; } }

        // Stuff for touchdown assist
        public DateTime LastAlphaTD { get; set; }
        public DateTime LastAlphaFumbi { get; set; }
        public ulong LastAlphaFumbiID { get; set; }
        public DateTime LastBetaTD { get; set; }
        public DateTime LastBetaFumbi { get; set; }
        public ulong LastBetaFumbiID { get; set; }

        public RoomCollection RoomCollection { get; set; }
        #endregion

        public Room(RoomCollection owner, EServerType serverType = EServerType.Game)
        {
            CreationTime = DateTime.Now;
            RoundStartTime = DateTime.Now;
            StartTime = DateTime.Now;
            RoomCollection = owner;
            ServerType = serverType;
        }

        public void Join(Player plr)
        {
            plr.Room = this;
            if (ServerType == EServerType.Game)
            {
                plr.ResetScore();
                plr.State = EPlayerState.Lobby;
            }
            _players.TryAdd(plr.Session.Guid, plr);

            if (ServerType != EServerType.Game) return;

            var ack = new Packet(EGamePacket.SEnterRoomSuccessAck);
            ack.Write(ID);
            ack.Write(MatchKey);
            ack.Write((uint)State);
            ack.Write((uint)TimeState);
            ack.Write(TimeLimit);

            var ts = DateTime.Now - StartTime;
            if(State == EGameRuleState.Waiting || State == EGameRuleState.Result)
                ack.Write((uint)0);
            else
                ack.Write((uint)ts.TotalMilliseconds); // time passed
            ack.Write(ScoreLimit);
            ack.Write(IsFriendly);
            ack.Write(IsBalanced);
            ack.Write(MinLevel);
            ack.Write(MaxLevel);
            ack.Write(EquipLimit);
            ack.Write(IsNoIntrusion);
            plr.Session.Send(ack);

            // Generate unique slot id!
            var slotID = 2;
            while (FindPlayerInSlot(slotID) != null)
                slotID++;
            plr.SlotID = (byte)slotID;

            ack = new Packet(EGamePacket.SIdsInfoAck);
            ack.Write(plr.SlotID);
            ack.Write(TunnelID);
            ack.Write((uint)0);
            plr.Session.Send(ack);

            ack = new Packet(EGamePacket.SPlayerEnteredAck);
            ack.Write((byte)0x00);
            ack.Write((byte)_players.Count);

            foreach (var player in _players.Values)
            {
                ack.Write(player.PrivateIP); // private ip
                ack.Write(player.PrivatePort); // private port
                ack.Write(player.PublicIP); // public ip
                ack.Write(player.PublicPort); // public port
                ack.Write(player.NATUnk);
                ack.Write(player.ConnectionType); // connection type | 6 relay
                ack.Write(player.AccountID);
                ack.Write(player.SlotID);
                ack.Write((uint)0);
                ack.Write((byte)0x01);
                ack.WriteStringBuffer(player.Nickname, 31);
            }
            Broadcast(ack);
        }

        public void Leave(Player plr, byte leaveType = 0)
        {
            Packet ack;
            if (ServerType == EServerType.Game)
            {
                ack = new Packet(EGamePacket.SRoomPlayerLeave);
                ack.Write(plr.AccountID);
                ack.WriteStringBuffer(plr.Nickname, 31);
                ack.Write(leaveType); // leave reason | 1 = kick
                Broadcast(ack);

                ack = new Packet(EGamePacket.SPlayerLeaveAck);
                ack.Write(plr.AccountID);
                ack.Write(plr.SlotID);
                Broadcast(ack);
            }

            Player tmp;
            if (!_players.TryRemove(plr.Session.Guid, out tmp)) return;
            plr.IsReady = false;
            plr.State = EPlayerState.Lobby;
            plr.SlotID = 0;
            plr.Room = null;
            plr.Team = ETeam.Neutral;
            plr.GameMode = EPlayerGameMode.Normal;
            plr.ResetScore();

            if (_players.Count == 0)
            {
                Room roomTEMP;
                RoomCollection.TryRemove(TunnelID, out roomTEMP);

                if (Channel == null || ServerType != EServerType.Game) return;
                ack = new Packet(EGamePacket.SDisposeRoomAck);
                ack.Write(ID);
                Channel.Broadcast(ack);
                return;
            }

            if (ServerType != EServerType.Game) return;
            if (MasterID == plr.AccountID) // master left, we need a new one
            {
                MasterID = _players.First().Value.AccountID;
                ack = new Packet(EGamePacket.SRoomChangeRefereeAck);
                ack.Write(MasterID);
                Broadcast(ack);

                ack = new Packet(EGamePacket.SRoomChangeMasterAck);
                ack.Write(MasterID);
                Broadcast(ack);
            }

            if (State == EGameRuleState.Playing && ( (CountInTeam(ETeam.Alpha) == 0 || CountInTeam(ETeam.Beta) == 0) 
                || (CountInTeamPlaying(ETeam.Alpha) == 0 || CountInTeamPlaying(ETeam.Beta) == 0) ))
                BeginResult();
        }

        public int CountInTeam(ETeam team, EPlayerGameMode gameMode = EPlayerGameMode.Normal)
        {
            var res = _players.Values.Where(plr => plr.Team == team && plr.GameMode == gameMode);
            return res.Count();
        }
        public int CountInTeamReady(ETeam team)
        {
            var res = from plr in _players.Values
                      where plr.Team == team && (plr.IsReady || MasterID == plr.AccountID) && plr.GameMode != EPlayerGameMode.Spectate
                      select plr;
            return res.Count();
        }
        public int CountInTeamPlaying(ETeam team)
        {
            var res = from plr in _players.Values
                      where plr.Team == team && (plr.State == EPlayerState.Alive || plr.State == EPlayerState.Dead || plr.State == EPlayerState.Waiting)
                      select plr;
            return res.Count();
        }
        public int CountSpectate()
        {
            var res = _players.Values.Where(plr => plr.GameMode == EPlayerGameMode.Spectate);
            return res.Count();
        }

        public Player FindPlayerInSlot(int slot)
        {
            var res = _players.Values.Where(plr => plr.SlotID == slot);
            var players = res as IList<Player> ?? res.ToList();
            return !players.Any() ? null : players.First();
        }

        public void Broadcast(Packet packet, ulong excludeID = 0)
        {
            Broadcast(packet.GetData(), excludeID);
        }
        public void Broadcast(byte[] packet, ulong excludeID = 0)
        {
            foreach (var player in _players.Values.Where(player => player.AccountID != excludeID))
            {
                player.Session.Send(packet);
            }
        }

        public void BroadcastEventMessage(EPlayerEventMessage msg, string str = null, long param1 = -1, uint param2 = 0, ushort param3 = 0)
        {
            foreach (var plr in _players.Values)
            {
                if (msg == EPlayerEventMessage.StartGame && (!plr.IsReady && plr.AccountID != MasterID))
                    continue;
                var ack = new Packet(EGamePacket.SEventMessageAck);
                if(param1 == -1)
                    ack.WriteEventMessage(plr.AccountID, msg, str, param2, param3);
                else
                    ack.WriteEventMessage((ulong)param1, msg, str, param2, param3);
                plr.Session.Send(ack);
            }
        }

        async public void ChangeTimeState(EGameTimeState state)
        {
            TimeState = state;
            var ack = new Packet(EGamePacket.SRoomChangeSubState);
            ack.Write((uint)state);
            if (state == EGameTimeState.HalfTime)
            {
                await Task.Factory.StartNew(() =>
                {
                    for (var i = 10; i > -1; i--) // 10 seconds
                    {
                        BroadcastEventMessage(EPlayerEventMessage.HalfTimeIn, i.ToString(), 2);
                        Task.Delay(1000).Wait();
                    }
                });
                Broadcast(ack);

                await Task.Delay(25000); // 25 seconds half time
                ChangeTimeState(EGameTimeState.SecondHalf);
                RoundStartTime = DateTime.Now;
                return;
            }
            Broadcast(ack);
        }

        public void ChangeRoomState(EGameRuleState state)
        {
            State = state;
            var ack = new Packet(EGamePacket.SRoomChangeStateAck);
            ack.Write((uint)State);
            Broadcast(ack);
        }

        public byte GetPing()
        {
            var ping = Players.Values.Aggregate<Player, uint>(0, (current, plr) => current + plr.Ping);
            if (ping == 0)
                return 50;

            ping /= (uint)Players.Count;

            var percentage = (byte)(100 - (((ping / 80) * 100) - 100));
            //je kleiner der ping (z.b. 15) desto größer der wert
            //wenn der ping über 80 geht dann fängt er an runter zu zählen(bei 120 ping sinds nur noch 60%)

            return (byte)((percentage > 100) ? 100 : percentage);
        }

        public byte GetConnectingCount()
        {
            var count = (byte) _players.Values.Count(plr => !plr.IsSpawned);
            return count;
        }

        private ETeam GetWinTeam()
        {
            var winTeam = ETeam.Alpha;
            if (GameRule == EGameRule.Survival)
                return winTeam;

            if (ScoreAlpha == ScoreBeta)
            {
                uint scoreA = 0;
                uint scoreB = 0;
                foreach (var plr in _players.Values.Where(plr => plr.State == EPlayerState.Alive))
                {
                    switch (plr.Team)
                    {
                        case ETeam.Alpha:
                            switch (GameRule)
                            {
                                case EGameRule.Touchdown:
                                    scoreA += plr.GameScore.TotalPoints;
                                    break;
                                case EGameRule.Deathmatch:
                                    scoreA += plr.GameScore.TotalPoints;
                                    break;
                            }
                            break;

                        case ETeam.Beta:
                            switch (GameRule)
                            {
                                case EGameRule.Touchdown:
                                    scoreB += plr.GameScore.TotalPoints;
                                    break;
                                case EGameRule.Deathmatch:
                                    scoreB += plr.GameScore.TotalPoints;
                                    break;
                            }
                            break;
                    }
                }
                if (scoreA > scoreB)
                    winTeam = ETeam.Alpha;
                else if (scoreB > scoreA)
                    winTeam = ETeam.Beta;
            }
            else if (ScoreAlpha > ScoreBeta)
                winTeam = ETeam.Alpha;
            else if (ScoreBeta > ScoreAlpha)
                winTeam = ETeam.Beta;

            return winTeam;
        }

        async public void BeginResult()
        {
            ChangeRoomState(EGameRuleState.Result);
            BroadcastBriefing(true);

            var res = _players.Values.Where(e => e.State != EPlayerState.Lobby && e.State != EPlayerState.Spectating);
            foreach (var e in res)
                e.ResetScore();

            await Task.Delay(20000); // 20 seconds result screen

            EndResult();

            ScoreAlpha = 0;
            ScoreBeta = 0;
            LastAlphaFumbiID = 0;
            LastBetaFumbiID = 0;
        }

        public void EndResult()
        {
            _players.ChangeState(EPlayerState.Lobby);
            ChangeRoomState(EGameRuleState.Waiting);
            BroadcastBriefing();
        }

        public void BeginRound()
        {
            var res = _players.Values.Where(plr => plr.IsReady || plr.AccountID == MasterID);
            foreach (var plr in res)
            {
                plr.State = plr.GameMode == EPlayerGameMode.Normal ? EPlayerState.Alive : EPlayerState.Spectating;
                plr.GameScore.JoinTime = DateTime.Now;
                plr.IsReady = false;
            }

            BroadcastBriefing();
            StartTime = DateTime.Now;
            RoundStartTime = DateTime.Now;
            ChangeTimeState(EGameTimeState.FirstHalf);
            ChangeRoomState(EGameRuleState.Playing);
        }

        public void Update()
        {
            var tmp = (int) ( GameRule == EGameRule.Survival ? TimeLimit : TimeLimit/2 );
            var roundTime = new TimeSpan(0, 0, 0, 0, tmp);
            var diff = DateTime.Now - RoundStartTime;

            if (diff.TotalSeconds >= roundTime.TotalSeconds)
            {
                if (GameRule == EGameRule.Survival)
                {
                    BeginResult();
                    return;
                }
                switch (TimeState)
                {
                    case EGameTimeState.FirstHalf:
                        ChangeTimeState(EGameTimeState.HalfTime);
                        break;
                    case EGameTimeState.SecondHalf:
                        BeginResult();
                        break;
                }
            }

            var halfTimeScoreLimit = ScoreLimit / 2;
            if ((ScoreAlpha >= halfTimeScoreLimit || ScoreBeta >= halfTimeScoreLimit) && TimeState == EGameTimeState.FirstHalf)
                ChangeTimeState(EGameTimeState.HalfTime);
            if ((ScoreAlpha >= ScoreLimit || ScoreBeta >= ScoreLimit) && TimeState == EGameTimeState.SecondHalf)
                BeginResult();
        }

        public void BroadcastBriefing(bool result = false)
        {
            var ack = new Packet(EGamePacket.SRoomBriefingAck);
            var winTeam = ETeam.Neutral;
            if (!result)
            {
                ack.Write((byte)0);
                ack.Write((byte)0);
                ack.Write((uint)0);
            }
            else
            {
                winTeam = GetWinTeam();
                ack.Write((byte)1);
                ack.Write((byte)1);
                ack.Write((uint)winTeam); // WINNING TEAM 
            }

            var res = _players.Values.Where(e => e.GameMode == EPlayerGameMode.Spectate);
            var spectators = res as IList<Player> ?? res.ToList();

            #region Count infos
            ack.Write((uint)2); // 2 TEAMS
            ack.Write(Players.Count);
            ack.Write(spectators.Count());

            ack.Write((byte)ETeam.Alpha);
            ack.Write(ScoreAlpha);
            ack.Write((byte)ETeam.Beta);
            ack.Write(ScoreBeta);
            #endregion

            #region Player infos
            foreach (var plr in Players.Values)
            {
                uint exp = 0;
                uint pen = 0;

                #region result
                if (result)
                {
                    if (plr.State == EPlayerState.Lobby || plr.State == EPlayerState.Spectating)
                        continue;

                    plr.UpdateEuqip();

                    switch (GameRule)
                    {
                        case EGameRule.Touchdown:
                            pen = plr.GameScore.CalculateEXP();
                            exp = pen * 2;
                            plr.SetNewTDStats((plr.Team == winTeam));
                            break;

                        case EGameRule.Deathmatch:
                            pen = plr.GameScore.CalculateEXP();
                            exp = pen * 2;
                            plr.SetNewDMStats((plr.Team == winTeam));
                            break;

                        case EGameRule.Survival:
                            break;
                    }

                    if (Player.LevelSystem.ContainsKey((int) plr.Level))
                    {
                        var expMax = Player.LevelSystem[(int)plr.Level];
                        var newExp = plr.EXP + exp;
                        if (newExp > expMax)
                        {
                            plr.Level++;
                            plr.EXP = (uint)(newExp - expMax);
                        }
                        else
                        {
                            plr.EXP = newExp;
                        }
                        plr.PEN += pen;
                        GameDatabase.Instance.UpdateMoney(plr);
                        GameDatabase.Instance.UpdateEXPLevel(plr);
                    }
                }
                #endregion

                ack.Write(plr.AccountID);
                ack.Write((byte)plr.Team);
                ack.Write((byte)plr.State);
                ack.Write(plr.IsReady);
                ack.Write((int)plr.GameMode);
                ack.Write(plr.GameScore.TotalPoints);
                ack.Write((uint)0);
                ack.Write(pen);
                ack.Write(exp);
                ack.Write((uint)plr.CalculateTotalEXP());
                ack.Write((byte)0); // flag for (Event, etc) 1 ->  [event; lvl up]
                ack.Write((uint)0); // extra exp (+x)
                ack.Write((uint)0); // extra pen (+x)
                ack.Write((byte)0);
                ack.Write((uint)0);
                ack.Write((byte)0);
                ack.Write((byte)0);

                #region scores
                switch (GameRule)
                {
                    case EGameRule.Touchdown:
                        var tdGame = (TDGameScore) plr.GameScore;
                        ack.Write(tdGame.TDScore);
                        ack.Write(tdGame.Kills);
                        ack.Write(tdGame.Kills);
                        ack.Write(tdGame.Kills);// kill 
                        ack.Write(tdGame.Defense);
                        ack.Write(tdGame.Defense);
                        ack.Write(tdGame.Defense); //defensive
                        ack.Write(tdGame.Defense);  //defensive
                        ack.Write((uint)0); // heal points
                        ack.Write((uint)0);
                        ack.Write((uint)0);
                        ack.Write((uint)0);
                        ack.Write(tdGame.Offense); // offense point
                        ack.Write((uint)0);
                        ack.Write((uint)0);
                        break;
                    case EGameRule.Deathmatch:
                        var dmGame = (DMGameScore)plr.GameScore;
                        ack.Write(dmGame.Kills >> 1); // kill points
                        ack.Write(dmGame.Kills); // kill assist points
                        ack.Write((uint)0); //heal points
                        ack.Write((uint)0);
                        ack.Write((uint)0);
                        ack.Write((uint)0);
                        ack.Write(dmGame.Deaths);
                        break;

                    case EGameRule.Survival:
                        var sGame = (SurvivalGameScore) plr.GameScore;
                        ack.Write(sGame.Kills);
                        break;
                }
                #endregion
            }
            #endregion

            foreach (var plr in spectators)
            {
                ack.Write(plr.AccountID);
                ack.Write(0);
            }

            Broadcast(ack);
        }
    }
}
