using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FagNet.Core.Constants;
using FagNet.Core.Constants.Packets;
using FagNet.Core.Cryptography;
using FagNet.Core.Data;
using FagNet.Core.Database;
using FagNet.Core.Network;
using FagNet.Core.Network.Events;
using FagNet.Core.Utils;

namespace FagNetGame
{
    public class GameServer
    {
        public static GameServer Instance { get { return Singleton<GameServer>.Instance; } }

        private readonly PacketLogger _packetLogger;
        public Logger Logger { get; private set; }

        private readonly TcpServer _server;
        private readonly RemoteClient _authRemoteClient;

        public PlayerCollection Players { get; private set; }
        public RoomCollection Rooms { get; private set; }
        public ChannelCollection Channels { get; private set; }

        private readonly CancellationTokenSource _roomHandlerCancellationTokenSource = new CancellationTokenSource();
        private Task _roomHandlerTask;

        private readonly PluginManager _pluginManager = new PluginManager();

        public GameServer()
        {
            Channels = new ChannelCollection();
            Rooms = new RoomCollection();
            Players = new PlayerCollection();
            Logger = new Logger() { WriteToConsole = true };
            _packetLogger = new PacketLogger();
            Logger.Load(Path.Combine("logs", string.Format("game_{0}.log", DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss"))));
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Error(s, new ExceptionEventArgs((Exception)e.ExceptionObject));
                Environment.Exit(0);
            };

            _packetLogger.Load("game_packets.log");

            Logger.Info("Loading game_config.xml...");
            GameConfig.Load();
            Logger.Info("Setting up servers...");
            _server = new TcpServer(IPAddress.Parse(GameConfig.Instance.IP), GameConfig.Instance.Port);
            _server.PacketReceived += HandlePacket;
            _server.ClientDisconnected += ClientDisconnected;
            _server.Error += Error;

            var isMono = Type.GetType("Mono.Runtime") != null;
            switch (GameConfig.Instance.AuthRemote.Binding)
            {
                case "pipe":
                    if (isMono)
                    {
                        Logger.Error("pipe is not supported in mono, use http!");
                        Environment.Exit(1);
                        return;
                    }
                    _authRemoteClient = new RemoteClient(ERemoteBinding.Pipe, string.Format("localhost/FagNetAuth/{0}/", SHA256.ComputeHash(GameConfig.Instance.AuthRemote.Password)));
                    break;

                case "tcp":
                    if (isMono)
                    {
                        Logger.Error("pipe is not supported in mono, use http!");
                        Environment.Exit(1);
                        return;
                    }
                    _authRemoteClient = new RemoteClient(ERemoteBinding.Pipe, string.Format("{0}:{1}/FagNetAuth/{2}/", GameConfig.Instance.AuthRemote.Server, GameConfig.Instance.AuthRemote.Port, SHA256.ComputeHash(GameConfig.Instance.AuthRemote.Password)));
                    break;

                case "http":
                    _authRemoteClient = new RemoteClient(ERemoteBinding.Http, string.Format("{0}:{1}/FagNetAuth/{2}/", GameConfig.Instance.AuthRemote.Server, GameConfig.Instance.AuthRemote.Port, SHA256.ComputeHash(GameConfig.Instance.AuthRemote.Password)));
                    break;

                default:
                    Logger.Error("Invalid remote binding '{0}' for AuthRemote", GameConfig.Instance.AuthRemote.Binding);
                    Environment.Exit(1);
                    break;
            }

            Logger.Info("Loading plugins...");
            _pluginManager.Load();

            foreach (var plugin in _pluginManager.Plugins)
                Logger.Info("Loaded {0}", plugin.Name);
        }

        public void Start()
        {
            Logger.Info("Connecting to MySQL database...");
            try
            {
                GameDatabase.Instance.TryConnect(GameConfig.Instance.MySQLGame.Server, GameConfig.Instance.MySQLGame.User, GameConfig.Instance.MySQLGame.Password, GameConfig.Instance.MySQLGame.Database);
                AuthDatabase.Instance.TryConnect(GameConfig.Instance.MySQLAuth.Server, GameConfig.Instance.MySQLAuth.User, GameConfig.Instance.MySQLAuth.Password, GameConfig.Instance.MySQLAuth.Database);
                Channels = GameDatabase.Instance.GetChannels(EServerType.Game);

                GameDatabase.Instance.UpdateOnlineFlags();
            }
            catch (Exception ex)
            {
                Logger.Error("Could not connect to MySQL database: {0}\r\n{1}", ex.Message, ex.InnerException);
                Environment.Exit(0);
            }
            _server.Start();

            _roomHandlerTask = Task.Factory.StartNew(RoomHandler);
            _roomHandlerTask.ContinueWith((t) => { if (t.IsFaulted) Error(this, new ExceptionEventArgs(t.Exception)); });
            Logger.Info("Ready for connections!");
        }

        public void Stop()
        {
            Logger.Info("Shutting down...");
            _roomHandlerCancellationTokenSource.Cancel();
            _roomHandlerTask.Wait();
            _server.Stop();
            Logger.Dispose();
            _packetLogger.Dispose();
        }

        private void HandlePacket(object sender, PacketReceivedEventArgs e)
        {
            e.Packet.Decrypt();
            e.Packet.ReadBytes(4); // packet counter
            //_packetLogger.Log<EGamePacket>(e.Packet);

            if (_pluginManager.OnPacket(e.Session, e.Packet))
                return;

            switch (e.Packet.PacketID)
            {
                case (byte)EGamePacket.CLoginReq:
                    HandleLoginRequest(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CCreateCharacterReq:
                    HandleCreateCharacter(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CSelectCharacterReq:
                    HandleSelectCharacter(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CDeleteCharacterReq:
                    HandleDeleteCharacter(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CTimeSyncReq:
                    HandleTimeSync(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CKeepAliveReq:
                    break;

                case (byte)EGamePacket.CChannelInfoReq:
                    HandleChannelInformation(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CChannelEnterReq:
                    HandleChannelEnter(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CChannelLeaveReq:
                    HandleChannelLeave(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CGetPlayerInfoReq:
                    HandleGetPlayerInfo(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CNATInfoReq:
                    HandleNATInfo(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CBuyItemReq:
                    HandleBuyItem(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CRefundItemReq:
                    HandleRefundItem(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CRepairItemReq:
                    HandleRepairItem(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CRefreshItemsReq:
                    HandleRefreshItems(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CRefreshEQItemsReq:
                    HandleRefreshEQItems(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CClearInvalidateItemsReq:
                    HandleClearInvalidateItems(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CUseItemReq:
                    HandleUseItem(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CRegisterLicenseReq:
                    HandleRegisterLicense(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CCreateRoomReq:
                    HandleCreateRoom(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CJoinTunnelReq:
                    HandleJoinTunnel(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.SCRoomPlayerEnter:
                    HandlePlayerEntered(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CEnterRoomReq:
                    HandleEnterRoom(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CBeginRoundReq:
                    HandleBeginRound(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CRoomLeaveReq:
                    HandleLeaveRoom(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CEventMessageReq:
                    HandleEventMessage(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CRoomReadyReq:
                    HandleReadyRound(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CAdminShowWindowReq:
                    HandleAdminShowWindow(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CAdminActionReq:
                    HandleAdminAction(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CScoreKillReq:
                    HandleScoreKill(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.SScoreKillAssistReq:
                    HandleScoreKillAssist(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CReboundFumbiReq:
                    HandleReboundFumbi(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.SCTouchdown:
                    HandleTouchdown(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CScoreSuicideReq:
                    HandleSuicide(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CScoreOffenseReq:
                    HandleScoreOffense(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CScoreOffenseAssistReq:
                    HandleScoreOffenseAssist(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CScoreDefenseReq:
                    HandleScoreDefense(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CScoreDefenseAssistReq:
                    HandleScoreDefenseAssist(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CChangeTeamReq:
                    HandleChangeTeam(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CRoomKickReq:
                    HandleRoomKick(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CRandomshopReq:
                    HandleRandomShop(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CLogoutReq:
                    HandleLogout(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CRoomChangeItemsReq:
                    HandleRoomChangeItems(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CAvatarChangeReq:
                    HandleAvatarChange(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CChangeRoomReq:
                    HandleChangeRoom(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CRoomPlayerGameModeChangeReq:
                    HandleChangePlayerGameMode(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CTutorialCompletedReq:
                    HandleTutorialCompleted(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CScoreSurvivalReq:
                    HandleScoreSurvival(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CQuickJoinReq:
                    HandleQuickJoin(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.SCRoomMovePlayer:
                    HandleMovePlayer(e.Session, e.Packet);
                    break;

                case (byte)EGamePacket.CRoomShuffleReq:
                    HandleShuffle(e.Session, e.Packet);
                    break;

                default:
                    Logger.Warning("Unkown packet {0}", e.Packet.PacketID.ToString("x2"));
                    break;
            }
        }

        private void ClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            Player plr;
            Players.TryRemove(e.Session.Guid, out plr);
            if (plr == null) return;

            GameDatabase.Instance.UpdateOnlineFlag(plr.AccountID, false);
            var room = plr.Room;
            if (room != null)
                room.Leave(plr);

            if (plr.Channel != null)
                plr.Channel.Leave(plr);
        }

        private void Error(object sender, ExceptionEventArgs e)
        {
            Logger.Error(string.Format("{0}\r\n{1}", e.Exception.Message, e.Exception.StackTrace));
            if (e.Exception.InnerException != null)
                Logger.Error(string.Format("{0}\r\n{1}", e.Exception.InnerException.Message, e.Exception.InnerException.StackTrace));
        }

        private void RoomHandler()
        {
            var token = _roomHandlerCancellationTokenSource.Token;
            while (!token.IsCancellationRequested)
            {
                foreach (var room in Rooms.Values.Where(room => room.State == EGameRuleState.Playing && room.TimeState != EGameTimeState.HalfTime))
                {
                    if(_pluginManager.RoomTick(room))
                        continue;
                    room.Update();
                }

                Task.Delay(1000, token).Wait(token); // check every second
            }
        }

        private void HandleLoginRequest(TcpSession session, Packet p)
        {
            var ip = session.Client.Client.RemoteEndPoint as IPEndPoint;
            var username = p.ReadCStringBuffer(43);
            var sessionID = p.ReadUInt32();
            var accID = AuthDatabase.Instance.GetAccountID(username);
            Logger.Info("-CLoginReq- User: {0} ID: {1} SessionID: {2}", username, accID, sessionID);

            Packet ack;
            if (accID == 0 || !ValidateSession(sessionID, accID, ip.Address))
            {
                ack = new Packet(EGamePacket.SLoginAck);
                ack.Write((ulong)0);
                ack.Write((uint)5);
                session.Send(ack);
                session.StopListening();
                return;
            }

            var player = GameDatabase.Instance.GetPlayer(accID);
            if (player == null) // new player!
            {
                player = new Player
                {
                    Username = username,
                    Nickname = AuthDatabase.Instance.GetNickname(accID),
                    GMLevel = AuthDatabase.Instance.GetGMLevel(accID),
                    AccountID = accID,
                    PEN = GameConfig.Instance.StartPEN,
                    AP = GameConfig.Instance.StartAP,
                    DMStats = new DMStatistics(),
                    TDStats = new TDStatistics()
                };

                GameDatabase.Instance.CreatePlayer(player);
            }
            player.Session = session;
            player.SessionID = sessionID;
            GameDatabase.Instance.UpdateOnlineFlag(player.AccountID, true);

            if (Players.GetPlayerByID(player.AccountID) != null /* prevent multiple logins! */)
            {
                session.StopListening();
                return;
            }
            Players.TryAdd(session.Guid, player);

            ack = new Packet(EGamePacket.SLoginAck);
            ack.Write(player.AccountID);
            ack.Write((uint)0); // error code
            session.Send(ack);

            #region License info

            ack = new Packet(EGamePacket.SLicenseInfoAck);
            ack.Write((byte)100);
            for (var i = 1; i <= 100; i++)
                ack.Write((byte)i);
            //foreach (var license in player.Licenses)
            //ack.Write((byte)license);
            session.Send(ack);

            #endregion

            #region Character info

            ack = new Packet(EGamePacket.SCharSlotInfoAck);
            ack.Write((byte)player.Characters.Count); // num chars
            ack.Write((byte)3); // num charslots
            ack.Write(player.ActiveCharSlot); // active char slot
            session.Send(ack);

            for (var i = 0; i < player.Characters.Count; i++)
            {
                var character = player.Characters[i];

                ack = new Packet(EGamePacket.SOpenCharInfoAck);
                ack.Write(character.Slot);
                ack.Write((byte)0x01);
                ack.Write((byte)0x03);
                ack.Write((character.Avatar));
                session.Send(ack);

                ack = new Packet(EGamePacket.SCharEquipInfoAck);
                ack.Write(character.Slot);
                ack.Write((byte)1); // skill counter
                ack.Write((byte)3);

                for (var j = 0; j < 3; j++)
                {
                    for (var n = 0; n < 3; n++)
                    {
                        ack.Write((byte)n);
                        ack.Write(character.Weapons[n]);
                    }

                    ack.Write((byte)0);
                    ack.Write(character.Skill);
                }

                for (var j = 0; j < 7; j++)
                {
                    ack.Write((byte)j);
                    ack.Write(character.Clothes[j]);
                }

                session.Send(ack);
            }

            #endregion

            #region Inventory

            ack = new Packet(EGamePacket.SInventoryAck);
            ack.Write((uint)player.Inventory.Count);
            foreach (var item in player.Inventory)
            {
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
            }
            session.Send(ack);

            #endregion

            ack = new Packet(EGamePacket.SResultAck);
            ack.Write((uint)0x27);
            session.Send(ack);

            #region Account info

            ack = new Packet(EGamePacket.SBeginAccountInfoAck);
            ack.Write((byte)0x00);
            ack.Write((byte)player.Level);
            ack.Write((uint)(player.CalculateTotalEXP() + player.EXP));
            ack.Write((uint)0); // points??
            ack.Write((uint)(player.TutorialCompleted ? 3 : 0));
            ack.WriteStringBuffer(player.Nickname, 31);
            ack.Write((uint)0); // unk

            // dm stuff
            ack.Write(player.DMStats.Won); // wins?
            ack.Write(player.DMStats.Lost); // loses??
            ack.Write((uint)player.DMStats.CalculateWinRate() >> 1);
            ack.Write((uint)0);
            ack.Write((uint)0);
            ack.Write((uint)0);
            ack.Write((uint)0);
            ack.Write((uint)0);
            ack.Write((uint)0);

            // td score stuff
            ack.Write(player.TDStats.CalculateWinRate()); // wins??
            ack.Write(0); // loses?
            ack.Write(player.TDStats.TotalTouchdowns);
            ack.Write(20 * player.TDStats.TotalMatches);
            ack.Write(player.TDStats.TotalTouchdownAssists);
            ack.Write(player.TDStats.TotalKills);
            ack.Write(player.TDStats.TotalKillAssists);
            ack.Write(player.TDStats.TotalOffense);
            ack.Write(player.TDStats.TotalOffenseAssists);
            ack.Write(player.TDStats.TotalDefense);
            ack.Write(player.TDStats.TotalDefenseAssists);
            ack.Write(player.TDStats.TotalRecovery);

            ack.Write((uint)0); // Total / x / 2 ???
            ack.Write((uint)0); // unk, nothing happens
            ack.Write((uint)0); // super increase for total score??
            ack.Write((uint)0); // total score goes to 0??
            ack.Write((uint)0); // unk, nothing happens
            ack.Write((uint)0); // unk, nothing happens
            session.Send(ack);

            #endregion

            ack = new Packet(EGamePacket.SResultAck);
            ack.Write((uint)0x11);
            session.Send(ack);
        }

        private void HandleNATInfo(TcpSession session, Packet p)
        {
            var ip = (IPEndPoint) session.Client.Client.RemoteEndPoint;
            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }

            plr.PrivateIP = p.ReadUInt32();
            plr.PrivatePort = p.ReadUInt16();

            plr.PublicIP = p.ReadUInt32();
            plr.PublicPort = p.ReadUInt16();

            // ignore public stuff from client
            plr.PublicIP = (uint)ip.Address.Address;
            plr.PublicPort = plr.PrivatePort;

            plr.NATUnk = p.ReadUInt16();
            plr.ConnectionType = p.ReadByte();
            if (ip.Address.ToString() == "127.0.0.1")
                plr.ConnectionType = 1;
            if (plr.ConnectionType == 6)
                plr.ConnectionType = 4;

            Logger.Info("-CNATInfoReq- NATUnk: {0} Type: {1}", plr.NATUnk, plr.ConnectionType);
        }

        private void HandleCreateCharacter(TcpSession session, Packet p)
        {
            var slot = p.ReadByte();
            var avatar = p.ReadUInt32();
            //_logger.Debug("-C_CREATE_CHARACTER_REQ- Slot: {0} Avatar: {1}", slot, avatar);

            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            if (plr.Characters.Count >= 3) // no cheating my friend!
            {
                session.StopListening();
                return;
            }

            var character = new Character {Slot = slot, Avatar = avatar};
            plr.Characters.Add(character);
            GameDatabase.Instance.CreateCharacter(plr.AccountID, slot, avatar);

            var ack = new Packet(EGamePacket.SCreateCharacterAck);
            ack.Write(slot);
            ack.Write(avatar);
            ack.Write((byte)1); // SKILL COUNT
            ack.Write((byte)3); // WEAPON COUNT
            session.Send(ack);
        }

        private void HandleSelectCharacter(TcpSession session, Packet p)
        {
            var slot = p.ReadByte();
            //_logger.Debug("-C_SELECT_CHARACTER_REQ- Slot: {0}", slot);

            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            var res = plr.Characters.Where(c => c.Slot == slot);
            if (!res.Any()) // cheater
            {
                session.StopListening();
                return;
            }

            GameDatabase.Instance.SetActiveCharSlot(plr.AccountID, slot);
            var ack = new Packet(EGamePacket.SSelectCharacterAck);
            ack.Write(slot);
            session.Send(ack);
        }

        private void HandleDeleteCharacter(TcpSession session, Packet p)
        {
            var slot = p.ReadByte();
            //_logger.Debug("-C_SELECT_CHARACTER_REQ- Slot: {0}", slot);

            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            var res = plr.Characters.Where(c => c.Slot == slot);
            if (!res.Any()) // cheater
            {
                session.StopListening();
                return;
            }

            var character = plr.Characters.First(c => c.Slot == slot);
            if (character == null)
            {
                session.StopListening();
                return;
            }
            plr.Characters.Remove(character);

            GameDatabase.Instance.DeleteCharacter(plr.AccountID, slot);
            var ack = new Packet(EGamePacket.SDeleteCharacterAck);
            ack.Write(slot);
            session.Send(ack);
        }

        private void HandleAvatarChange(TcpSession session, Packet p)
        {
            // TODO
            //ulong accountID = p.ReadUInt64();

            ////Costume Ids                           
            //for (int i = 0; i < 7; i++)
            //    p.ReadInt32();

            ////Skill ids (Action)
            //for (int i = 0; i < 2; i++)
            //    p.ReadInt32();

            ////Weapon Ids
            //for (int i = 0; i < 3; i++)
            //    p.ReadInt32();


            //_logger.Debug("-C_AVATAR_CHANGE_REQ-");

            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }

            var data = p.ReadToEnd();
            var ack = new Packet(EGamePacket.SAvatarChangeAck);
            ack.Write(data);
            plr.Room.Broadcast(ack, plr.AccountID) ;
        }

        private void HandleTimeSync(TcpSession session, Packet p)
        {
            var time = p.ReadUInt32();
            var ts = DateTime.Now - Process.GetCurrentProcess().StartTime;

            var ack = new Packet(EGamePacket.STimeSyncAck);
            ack.Write(time);
            ack.Write((uint)ts.TotalMilliseconds);
            session.Send(ack);

            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr)) return;
            plr.Ping = time - plr.LastSyncTime - 3000;
            plr.LastSyncTime = time;

        }

        async private void HandleChannelEnter(TcpSession session, Packet p)
        {
            var id = p.ReadUInt32();
            //_logger.Debug("-C_CHAN_ENTER_REQ- ID: {0}", id);

            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }

            Channel channel;
            if (!Channels.TryGetValue((ushort)id, out channel))
                return;
            channel.Join(plr);

            var ack = new Packet(EGamePacket.SResultAck);
            ack.Write((uint)5);
            session.Send(ack);

            ack = new Packet(EGamePacket.SCashUpdateAck);
            ack.Write(plr.PEN);
            ack.Write(plr.AP);
            session.Send(ack);

            //if (plr.ConnectionType == 0x06)
            //    Alice.SendMessageTo(plr.AccountID, "Mit deinem Router ist es leider nicht möglich hier zu spielen :(");
            //else if (plr.ConnectionType == 0x04)
            //    Alice.SendMessageTo(plr.AccountID, "Mit deinem Router kannst du leider nur einen Raum erstellen :(");

            await Task.Delay(1000);
            SendRoomList(session);
        }

        private void HandleChannelLeave(TcpSession session, Packet p)
        {
            //_logger.Debug("-C_CHAN_LEAVE_REQ-");

            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }

            if (plr.Channel == null)
                return;
            plr.Channel.Leave(plr);

            var ack = new Packet(EGamePacket.SResultAck);
            ack.Write((uint)8);
            session.Send(ack);
        }

        private void HandleChannelInformation(TcpSession session, Packet p)
        {
            var t = p.ReadByte();
            //_logger.Debug("-C_CHAN_INFO_REQ- Type: {0}", t);

            switch (t)
            {
                case 5: // channel info
                    var ack = new Packet(EGamePacket.SChannelInfoAck);
                    ack.Write((ushort)Channels.Count);
                    foreach (var channel in Channels.Values)
                    {
                        ack.Write(channel.ID);
                        ack.Write((ushort)channel.Players.Count);
                    }
                    session.Send(ack);
                    break;

                case 4: // room info
                    SendRoomList(session);
                    break;

                case 3: // ??
                    break;
            }
        }

        private void HandleBuyItem(TcpSession session, Packet p)
        {
            int count = p.ReadByte();
            Player player;
            if (!Players.TryGetValue(session.Guid, out player))
            {
                session.StopListening();
                return;
            }

            var buyTime = DateTime.Now;
            var itemsToBuy = new List<Tuple<Item, ShopItem>>();
            uint penCost = 0;
            uint apCost = 0;

            for (var i = 0; i < count; i++)
            {
                var item = new Item();
                var mixedID = p.ReadBytes(4);
                item.Category = mixedID[0];
                item.SubCategory = mixedID[1];
                item.ItemID = BitConverter.ToUInt16(mixedID, 2);
                item.ProductID = p.ReadByte();
                item.EffectID = p.ReadUInt32();
                item.PurchaseTime = HelperUtils.GetUnixTimestamp(buyTime);

                var shopItem = GameDatabase.Instance.GetShopItem(item.Category, item.SubCategory, item.ItemID, item.ProductID);
                if (shopItem == null) // hacker
                {
                    Logger.Error("-CBuyItemReq FAILED(HAX)- ItemID: {0} Category: {1} SubCategory: {2} Type: {3} EffectID: {4}", item.ItemID, item.Category, item.SubCategory, item.ProductID, item.EffectID);
                    session.StopListening();
                    return;
                }
                item.Energy = shopItem.Energy;
                item.ExpireTime = (shopItem.Time == -1) ? -1 : HelperUtils.GetUnixTimestamp(buyTime.AddSeconds(shopItem.Time));

                penCost += shopItem.Price;
                apCost += shopItem.Cash;
                itemsToBuy.Add(new Tuple<Item, ShopItem>(item, shopItem));
            }

            Packet ack;
            if (player.PEN < penCost || player.AP < apCost)
            {
                ack = new Packet(EGamePacket.SBuyItemAck);
                ack.Write((byte)EBuyItemResult.NotEnoughMoney);
                session.Send(ack);
                return;
            }

            _pluginManager.OnBuyItem(player, itemsToBuy.Select(e => e.Item1).ToList());

            foreach (var tuple in itemsToBuy)
            {
                var item = tuple.Item1;
                var shopItem = tuple.Item2;
                
                //_logger.Debug("-C_BUY_ITEM_REQ- ItemID: {0} Category: {1} SubCategory: {2} Type: {3} EffectID: {4}", item.ItemID, item.Category, item.SubCategory, item.ProductID, item.EffectID);

                player.PEN -= shopItem.Price;
                player.AP -= shopItem.Cash;

                var id = GameDatabase.Instance.CreateItem(item, player.AccountID);
                if (id == 0)
                {
                    ack = new Packet(EGamePacket.SBuyItemAck);
                    ack.Write((byte)EBuyItemResult.DBError);
                    session.Send(ack);
                    continue;
                }
                item.ID = id;
                item.SetupAPWeapon();
                player.AddItem(item);

                ack = new Packet(EGamePacket.SBuyItemAck);
                ack.Write((byte)EBuyItemResult.OK);
                ack.Write(item.Category);
                ack.Write(item.SubCategory);
                ack.Write(item.ItemID);
                ack.Write(item.ProductID);
                ack.Write(item.EffectID);
                ack.Write(item.ID);
                session.Send(ack);
            }
            player.UpdateMoney();
        }

        private void HandleRefundItem(TcpSession session, Packet p)
        {
            var itemID = p.ReadUInt64();
            Player player;
            if (!Players.TryGetValue(session.Guid, out player))
            {
                session.StopListening();
                return;
            }

            var itm = player.Inventory.FirstOrDefault(i => i.ID == itemID);
            if (itm == null)
            {
                session.StopListening();
                return;
            }

            player.Inventory.Remove(itm);
            GameDatabase.Instance.RemoveItem(itemID);

            var ack = new Packet(EGamePacket.SRefundItemAck);
            ack.Write((byte)0);
            ack.Write(itemID);
            session.Send(ack);

            player.PEN += itm.SellPrice;
            player.UpdateMoney();
        }

        private void HandleRepairItem(TcpSession session, Packet p)
        {
            var itemID = p.ReadUInt64();
            Player player;
            if (!Players.TryGetValue(session.Guid, out player))
            {
                session.StopListening();
                return;
            }

            var itm = player.Inventory.FirstOrDefault(i => i.ID == itemID);
            if (itm == null)
            {
                session.StopListening();
                return;
            }

            itm.Energy = itm.MaxEnergy;

            var ack = new Packet(EGamePacket.SRepairItemAck);
            ack.Write((byte)0);
            ack.Write(itemID);
            session.Send(ack);

            player.PEN -= 0; // repair cost
            player.UpdateMoney();
        }

        private void HandleRefreshItems(TcpSession session, Packet p)
        {
            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            var items = plr.Inventory;

            var res = items.Where(item => item.TimeLeft == 0);
            var invalidateItems = res.ToList();

            //_logger.Debug("-C_REFRESH_ITEMS_REQ- Count: {0}", invalidateItems.Count);

            var ack = new Packet(EGamePacket.SRefreshInvalidateItemsAck);
            ack.Write((byte)invalidateItems.Count);
            foreach (var item in invalidateItems)
            {
                ack.Write(item.ID);
            }
            session.Send(ack);
        }

        private void HandleRefreshEQItems(TcpSession session, Packet p)
        {
            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            var items = plr.Inventory;
            var res = items.Where(item => item.TimeLeft == 0);
            var ls = res as IList<Item> ?? res.ToList();
            //_logger.Debug("-C_REFRESH_EQ_ITEMS_REQ- Count: {0}", ls.Count);

            var ack = new Packet(EGamePacket.SRefreshInvalidateEQItemsAck);
            ack.Write((byte)ls.Count());
            foreach (var item in ls)
            {
                ack.Write(item.ID);
            }
            session.Send(ack);
        }

        private void HandleClearInvalidateItems(TcpSession session, Packet p)
        {
            //_logger.Debug("-C_CLEAR_INVALIDATE_ITEMS_REQ-");
            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            var res = plr.Inventory.Where(item => item.TimeLeft == 0);
            var items = res.ToList();
            var ack = new Packet(EGamePacket.SClearInvalidateItemsAck);
            ack.Write((byte)items.Count);

            foreach (var item in items)
            {
                foreach (var character in plr.Characters)
                {
                    for (var i = 0; i < character.Weapons.Length; i++)
                    {
                        if (character.Weapons[i] == item.ID)
                            character.Weapons[i] = 0;
                    }
                    for (var i = 0; i < character.Clothes.Length; i++)
                    {
                        if (character.Clothes[i] == item.ID)
                            character.Clothes[i] = 0;
                    }
                    if (character.Skill == item.ID)
                        character.Skill = 0;
                }
                
                Logger.Debug("-S_CLEAR_INVALIDATE_ITEMS_ACK- Item: {0}", item.ID);
                ack.Write(item.ID);
                plr.Inventory.Remove(item);
                GameDatabase.Instance.RemoveItem(item.ID);
            }
            session.Send(ack);
        }

        private void HandleUseItem(TcpSession session, Packet p)
        {
            var cmd = p.ReadByte();
            var charSlot = p.ReadByte();
            var eqSlot = p.ReadByte();
            var id = p.ReadUInt64();
            //_logger.Debug("-C_USE_ITEM_REQ- CMD: {0} CharSlot: {1} EQSlot: {2} ID: {3}", cmd, charSlot, eqSlot, id);

            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            var items = plr.Inventory;
            var res = items.Where(item => item.ID == id);
            var enumerable = res as IList<Item> ?? res.ToList();
            if (!enumerable.Any() || enumerable.Count() > 1 || charSlot > plr.Characters.Count) // fuu cheaters
            {
                session.StopListening();
                return;
            }
            var sitem = enumerable.First();
            var character = plr.Characters.First(e => e.Slot == charSlot);
            if (character == null)
            {
                session.StopListening();
                return;
            }

            if (cmd == 1) // equip
            {
                switch (sitem.Category)
                {
                    case 1:
                        character.Clothes[eqSlot] = id;
                        break;
                    case 2:
                        character.Weapons[eqSlot] = id;
                        break;
                    case 3:
                        character.Skill = id;
                        break;
                    default:
                        return;
                }
            }
            else // unequip
            {
                switch (sitem.Category)
                {
                    case 1:
                        character.Clothes[eqSlot] = 0;
                        break;
                    case 2:
                        character.Weapons[eqSlot] = 0;
                        break;
                    case 3:
                        character.Skill = 0;
                        break;
                    default:
                        return;
                }
            }
            GameDatabase.Instance.UpdateCharacterEquip(plr.AccountID, character);

            var ack = new Packet(EGamePacket.SUseItemAck);
            ack.Write(cmd);
            ack.Write(charSlot);
            ack.Write(eqSlot);
            ack.Write(id);
            session.Send(ack);
        }

        private void HandleRegisterLicense(TcpSession session, Packet p)
        {
            var id = p.ReadByte();
            //_logger.Debug("-C_REGISTER_LICENSE_REQ- ID: {0}", id);

            // ToDo Check id

            Player player;
            if (!Players.TryGetValue(session.Guid, out player))
            {
                session.StopListening();
                return;
            }
            var res = player.Licenses.Where(e => e == id);
            if (res.Any())
                return;

            GameDatabase.Instance.AddLicense(player.AccountID, id);
            player.Licenses.Add(id);

            var ack = new Packet(EGamePacket.SRefreshLicenseInfoAck);
            ack.Write(id);
            ack.Write((uint)0);
            session.Send(ack);

            // ToDo send item (5h)
        }

        private void HandleCreateRoom(TcpSession session, Packet p)
        {
            Player player;
            if (!Players.TryGetValue(session.Guid, out player))
            {
                session.StopListening();
                return;
            }
            if (player.Channel == null || player.PublicIP == 0)
                return;

            var room = new Room(Rooms);
            var roomID = Rooms.CreateRoomID(player.Channel.ID);
            var tunnelID = Rooms.CreateTunnelID();

            room.MasterID = player.AccountID;
            room.ID = roomID;
            room.TunnelID = tunnelID;
            room.Channel = player.Channel;
            room.Name = p.ReadCStringBuffer(31);
            room.MatchKey = p.ReadBytes(4);
            room.TimeLimit = p.ReadByte();
            room.TimeLimit *= 60 * 1000;
            room.ScoreLimit = p.ReadByte();
            room.Unk = p.ReadInt32();
            room.Password = p.ReadUInt32();
            room.IsFriendly = p.ReadBoolean();
            room.IsBalanced = p.ReadBoolean();
            room.MinLevel = p.ReadByte();
            room.MaxLevel = p.ReadByte();
            room.EquipLimit = p.ReadByte();
            room.IsNoIntrusion = p.ReadBoolean();
            room.State = EGameRuleState.Waiting;

            var cont = _pluginManager.OnCreateRoom(player, room);
            Packet ack;
            //_logger.Debug("-CCreateRoom- MapID: {0} Mode: {1}", room.MapID, (int)room.GameRule);

            if (!GameDatabase.Instance.IsValidMapID(room.MapID))
            {
                Logger.Error("-CCreateRoom HAX- NOT ALLOWED MapID: {0} Mode: {1}", room.MapID, (int)room.GameRule);
                ack = new Packet(EGamePacket.SResultAck);
                ack.Write((uint)EServerResult.FailedToRequestTask);
                session.Send(ack);
                return;
            }

            if (room.GameRule != EGameRule.Touchdown && room.GameRule != EGameRule.Deathmatch && room.GameRule != EGameRule.Survival && cont)
            {
                ack = new Packet(EGamePacket.SResultAck);
                ack.Write((uint)EServerResult.FailedToRequestTask);
                session.Send(ack);
                return;
            }
            Rooms.TryAdd(tunnelID, room);


            ack = new Packet(EGamePacket.SDeployRoomAck);
            ack.Write(room.ID);
            ack.Write(room.MatchKey);
            ack.Write((byte)room.State);
            ack.Write(room.GetPing()); 
            ack.WriteStringBuffer(room.Name, 31);
            ack.Write(room.PublicType);
            ack.Write(room.TimeLimit);
            ack.Write(room.ScoreLimit);
            ack.Write(room.IsFriendly);
            ack.Write(room.IsBalanced);
            ack.Write(room.MinLevel);
            ack.Write(room.MaxLevel);
            ack.Write(room.EquipLimit);
            ack.Write(room.IsNoIntrusion);

            player.Channel.Broadcast(ack);

            room.Join(player);
        }

        private void HandleJoinTunnel(TcpSession session, Packet p)
        {
            var slot = p.ReadByte();
            //_logger.Debug("-C_JOIN_TUNNEL_REQ- SlotID: {0}", slot);

            Player player;
            if (!Players.TryGetValue(session.Guid, out player))
            {
                session.StopListening();
                return;
            }
            if (player.Channel == null || player.Room == null)
                return;

            if (slot == 0)
            {
                player.Room.Leave(player);
            }

            var ack = new Packet(EGamePacket.SJoinTunnelAck);
            ack.Write(slot);
            session.Send(ack);
        }

        private void HandlePlayerEntered(TcpSession session, Packet p)
        {
            //_logger.Debug("-SC_PLAYER_ENTERED-");

            Player player;
            if (!Players.TryGetValue(session.Guid, out player))
            {
                session.StopListening();
                return;
            }

            var room = player.Room;
            if (room == null)
                return;

            var ack = new Packet(EGamePacket.SRoomChangeRefereeAck);
            ack.Write(room.MasterID);
            session.Send(ack);

            ack = new Packet(EGamePacket.SRoomChangeMasterAck);
            ack.Write(room.MasterID);
            session.Send(ack);

            var numAlpha = room.CountInTeam(ETeam.Alpha);
            var numBeta = room.CountInTeam(ETeam.Beta);

            if (numAlpha < numBeta)
                player.Team = ETeam.Alpha;
            else if (numAlpha > numBeta)
                player.Team = ETeam.Beta;
            else
                player.Team = ETeam.Alpha;

            ack = new Packet(EGamePacket.SCRoomPlayerEnter);
            ack.Write(player.AccountID);
            ack.Write((byte)player.Team);
            ack.Write((byte)player.GameMode); // 1 normal, 2 specate
            ack.Write((uint)player.CalculateTotalEXP()); // exp
            ack.WriteStringBuffer(player.Nickname, 31);
            room.Broadcast(ack);

            room.BroadcastBriefing();
        }

        private void HandleEnterRoom(TcpSession session, Packet p)
        {
            var roomID = p.ReadUInt32();
            var password = p.ReadUInt32();
            var gameMode = (EPlayerGameMode)p.ReadByte();
            //_logger.Debug("-C_ENTER_ROOM_REQ- ID: {0} PasswordCRC: {1} GameMode: {2}", roomID, password, gameMode);

            Player player;
            if (!Players.TryGetValue(session.Guid, out player))
            {
                session.StopListening();
                return;
            }
            if (player.Channel == null || player.PublicIP == 0)
                return;

            var room = Rooms.GetRoomByID(player.Channel.ID, roomID);
            if (room == null)
                return;

            if (room.Players.Count >= room.PlayerLimit)
            {
                Packet ack = new Packet(EGamePacket.SResultAck);
                ack.Write((uint)EServerResult.ImpossibleToEnterRoom);
                session.Send(ack);
                return;
            }

            if (password != room.Password)
            {
                var ack = new Packet(EGamePacket.SResultAck);
                ack.Write((uint)EServerResult.PasswordError);
                session.Send(ack);
                return;
            }

            // TODO: I dont think this should be here
            //if (room.IsObserveEnabled)
            //{
            //    var ack = new Packet(EGamePacket.S_RESULT_ACK);
            //    ack.Write((uint)EServerResult.SelectGameMode);
            //    session.Send(ack);
            //    return;
            //}

            player.GameMode = EPlayerGameMode.Normal;
            room.Join(player);
        }

        private void HandleBeginRound(TcpSession session, Packet p)
        {
            //_logger.Debug("-C_BEGINROUND_REQ-");

            Player player;
            if (!Players.TryGetValue(session.Guid, out player))
            {
                session.StopListening();
                return;
            }

            var room = player.Room;
            if (room == null)
                return;
            if (room.MasterID != player.AccountID || room.State != EGameRuleState.Waiting) // cheater...
                return;

            if (_pluginManager.OnBeginRound(player, room)) return;

            if ((room.CountInTeamReady(ETeam.Alpha) == 0 || room.CountInTeamReady(ETeam.Beta) == 0) && room.GameRule != EGameRule.Survival)
            {
                var ack = new Packet(EGamePacket.SEventMessageAck);
                ack.WriteEventMessage(player.AccountID, EPlayerEventMessage.CantStartGame);
                session.Send(ack);
                return;
            }

            room.BeginRound();
        }

        private void HandleLeaveRoom(TcpSession session, Packet p)
        {
            //_logger.Debug("-C_LEAVEROOM_REQ-");

            Player player;
            if (!Players.TryGetValue(session.Guid, out player))
            {
                session.StopListening();
                return;
            }
            var room = player.Room;
            if (room == null)
                return;

            room.Leave(player);
            SendRoomList(session);
        }

        private void HandleEventMessage(TcpSession session, Packet p)
        {
            var eventID = p.ReadByte();
            var accID = p.ReadUInt64();
            var unk1 = p.ReadUInt32();
            var unk2 = p.ReadUInt16();
            var strLen = p.ReadUInt32();
            var str = (strLen > 0) ? p.ReadCString() : string.Empty;

            //_logger.Debug("-C_EVENTMESSAGE_REQ- EventID: {0} AccID: {1} Unk1: {2} Unk2: {3} String: {4}", eventID, accID, unk1, unk2, str);

            var player = Players.GetPlayerByID(accID);
            Player sender;
            if (!Players.TryGetValue(session.Guid, out sender))
            {
                session.StopListening();
                return;
            }
            if (sender.Room == null)
                return;
            var room = sender.Room;

            if (player != null && player.State == EPlayerState.Lobby && eventID == (byte)EPlayerEventMessage.StartGame && room.State != EGameRuleState.Waiting)
            {
                player.State = player.GameMode == EPlayerGameMode.Normal ? EPlayerState.Alive : EPlayerState.Spectating;
                player.GameScore.JoinTime = DateTime.Now;
                room.BroadcastBriefing();
            }

            var ack = new Packet(EGamePacket.SEventMessageAck);
            ack.Write(eventID);
            ack.Write(accID);
            ack.Write(unk1);
            ack.Write(unk2);
            ack.Write(strLen);
            if (strLen > 0)
                ack.Write(str);
            room.Broadcast(ack);
        }

        private void HandleAdminShowWindow(TcpSession session, Packet p)
        {
            //_logger.Debug("-C_ADMIN_SHOWINDOW_REQ-");
            var ack = new Packet(EGamePacket.SAdminShowWindowAck);
            ack.Write((byte)0x00); // 0 = admin console active - >0 no console allowed
            session.Send(ack);
        }

        private void HandleAdminAction(TcpSession session, Packet p)
        {
            //_logger.Debug("-C_ADMIN_ACTION_REQ-");
            var cmd = p.ReadCString();
            if (!cmd.StartsWith("/"))
                return;

            Player player;
            if (!Players.TryGetValue(session.Guid, out player))
            {
                session.StopListening();
                return;
            }

            cmd = cmd.Remove(0, 1); // remove /
            var args = HelperUtils.ParseArgs(cmd);
            foreach (var plugin in _pluginManager.Plugins)
            {
                var ret = plugin.OnAdminAction(player, args);
                if(string.IsNullOrEmpty(ret))
                    continue;
                var ack = new Packet(EGamePacket.SAdminActionAck);
                ack.Write((byte)0x01); // does not matter
                ack.Write((ushort)ret.Length);
                ack.Write(ret);
                session.Send(ack);
            }
        }

        private void HandleReadyRound(TcpSession session, Packet p)
        {
            var isReady = p.ReadBoolean();
            //_logger.Debug("-C_READYROUND_REQ- IsReady: {0}", isReady);

            Player player;
            if (!Players.TryGetValue(session.Guid, out player))
            {
                session.StopListening();
                return;
            }
            var room = player.Room;
            if (room == null)
                return;

            if (_pluginManager.OnBeginRound(player, room)) return;

            player.IsReady = isReady;
            var ack = new Packet(EGamePacket.SRoomReadyAck);
            ack.Write(player.AccountID);
            ack.Write(isReady);
            room.Broadcast(ack);
        }

        private void HandleScoreKill(TcpSession session, Packet p)
        {
            var murderAccID = p.ReadUInt64();
            var unk1 = p.ReadUInt64(); // not sure
            var weaponID = p.ReadUInt32();
            var victimAccID = p.ReadUInt64();
            var unk3 = p.ReadUInt64(); // not sure

            //_logger.Debug("-C_SCORE_KILL_REQ- Murder: {0} Target: {1} Weapon: {2}", murderAccID, victimAccID, weaponID);

            var murder = Players.GetPlayerByID(murderAccID);
            var victim = Players.GetPlayerByID(victimAccID);
            if (murder == null || victim == null || murder.Room == null || victim.Room == null)
                return;
            if (murder.Room.TunnelID != victim.Room.TunnelID)
                return;

            var room = murder.Room;
            if (room.TDWaiting || room.TimeState == EGameTimeState.HalfTime)
                return;

            switch (room.GameRule)
            {
                case EGameRule.Touchdown:
                    var tdGame = (TDGameScore) murder.GameScore;
                    tdGame.TotalPoints += 2;
                    tdGame.Kills++;
                    break;
                case EGameRule.Deathmatch:
                    var dmGame = (DMGameScore)murder.GameScore;
                    dmGame.TotalPoints += 2;
                    dmGame.Kills++;
                    ((DMGameScore)victim.GameScore).Deaths++;
                    if (murder.Team == ETeam.Alpha)
                        room.ScoreAlpha++;
                    else
                        room.ScoreBeta++;
                    break;
            }

            var ack = new Packet(EGamePacket.SScoreKillAck);
            ack.Write(murderAccID);
            ack.Write(unk1);
            ack.Write(weaponID);
            ack.Write(victimAccID);
            ack.Write(unk3);
            ack.Write((byte)0x01);
            room.Broadcast(ack);
        }

        private void HandleScoreKillAssist(TcpSession session, Packet p)
        {
            var murderAccID = p.ReadUInt64();
            var unk1 = p.ReadUInt64(); // not sure
            var assistAccID = p.ReadUInt64();
            var unk2 = p.ReadUInt64(); // not sure
            var weaponID = p.ReadUInt32();
            var victimAccID = p.ReadUInt64();
            var unk3 = p.ReadUInt64(); // not sure

            //_logger.Debug("-C_SCORE_KILL_ASSIST- Murder: {0} Assist: {1} Target: {2} Weapon: {3}", murderAccID, assistAccID, victimAccID, weaponID);

            var murder = Players.GetPlayerByID(murderAccID);
            var assist = Players.GetPlayerByID(assistAccID);
            var victim = Players.GetPlayerByID(victimAccID);
            if (murder == null || victim == null || murder.Room == null || victim.Room == null || assist == null || assist.Room == null)
                return;
            if (murder.Room.TunnelID != victim.Room.TunnelID || murder.Room.TunnelID != assist.Room.TunnelID)
                return;

            var room = murder.Room;
            if (room.TDWaiting || room.TimeState == EGameTimeState.HalfTime)
                return;

            switch (room.GameRule)
            {
                case EGameRule.Touchdown:
                    var tdGame = (TDGameScore)murder.GameScore;
                    tdGame.TotalPoints += 2;
                    tdGame.Kills++;

                    tdGame = (TDGameScore)assist.GameScore;
                    tdGame.TotalPoints += 1;
                    tdGame.KillAssists++;
                    break;

                case EGameRule.Deathmatch:
                    var dmGame = (DMGameScore)murder.GameScore;
                    dmGame.TotalPoints += 2;
                    dmGame.Kills++;

                    dmGame = (DMGameScore)assist.GameScore;
                    dmGame.TotalPoints += 1;
                    dmGame.KillAssists++;
                    ((DMGameScore)victim.GameScore).Deaths++;

                    if (murder.Team == ETeam.Alpha)
                        room.ScoreAlpha++;
                    else
                        room.ScoreBeta++;
                    break;
            }

            var ack = new Packet(EGamePacket.SScoreKillAssistAck);
            ack.Write(murderAccID);
            ack.Write(unk1);
            ack.Write(assistAccID);
            ack.Write(unk2);
            ack.Write(weaponID);
            ack.Write(victimAccID);
            ack.Write(unk3);
            ack.Write((byte)0x00);
            room.Broadcast(ack);
        }

        private void HandleScoreOffense(TcpSession session, Packet p)
        {
            var murderAccID = p.ReadUInt64();
            var unk1 = p.ReadUInt64(); // not sure
            var weaponID = p.ReadUInt32();
            var victimAccID = p.ReadUInt64();
            var unk3 = p.ReadUInt64(); // not sure

            //_logger.Debug("-C_SCORE_OFFENSE_REQ- Murder: {0} Target: {1} Weapon: {2}", murderAccID, victimAccID, weaponID);


            var murder = Players.GetPlayerByID(murderAccID);
            var victim = Players.GetPlayerByID(victimAccID);
            if (murder == null || victim == null || murder.Room == null || victim.Room == null)
                return;
            if (murder.Room.TunnelID != victim.Room.TunnelID)
                return;


            var room = murder.Room;
            if (room.TDWaiting || room.TimeState == EGameTimeState.HalfTime)
                return;

            if (room.GameRule != EGameRule.Touchdown)
                return;

            var tdGame = (TDGameScore)murder.GameScore;
            tdGame.TotalPoints += 4;
            tdGame.Offense++;

            var ack = new Packet(EGamePacket.SScoreOffenseAck);
            ack.Write(murderAccID);
            ack.Write(unk1);
            ack.Write(weaponID);
            ack.Write(victimAccID);
            ack.Write(unk3);
            ack.Write((byte)0x00);
            room.Broadcast(ack);
        }

        private void HandleScoreOffenseAssist(TcpSession session, Packet p)
        {
            var murderAccID = p.ReadUInt64();
            var unk1 = p.ReadUInt64(); // not sure
            var assistAccID = p.ReadUInt64();
            var unk2 = p.ReadUInt64(); // not sure
            var weaponID = p.ReadUInt32();
            var victimAccID = p.ReadUInt64();
            var unk3 = p.ReadUInt64(); // not sure

            //_logger.Debug("-C_SCORE_OFFENSE_ASSIST_REQ- Murder: {0} Assist: {1} Target: {2} Weapon: {3}", murderAccID, assistAccID, victimAccID, weaponID);

            var murder = Players.GetPlayerByID(murderAccID);
            var assist = Players.GetPlayerByID(assistAccID);
            var victim = Players.GetPlayerByID(victimAccID);
            if (murder == null || victim == null || murder.Room == null || victim.Room == null || assist == null || assist.Room == null)
                return;
            if (murder.Room.TunnelID != victim.Room.TunnelID || murder.Room.TunnelID != assist.Room.TunnelID)
                return;

            var room = murder.Room;
            if (room.TDWaiting || room.TimeState == EGameTimeState.HalfTime)
                return;
            if (room.GameRule != EGameRule.Touchdown)
                return;

            var tdGame = (TDGameScore)murder.GameScore;
            tdGame.TotalPoints += 4;
            tdGame.Offense++;

            tdGame = (TDGameScore)assist.GameScore;
            tdGame.TotalPoints += 2;
            tdGame.OffenseAssists++;

            var ack = new Packet(EGamePacket.SScoreOffenseAssistAck);
            ack.Write(murderAccID);
            ack.Write(unk1);
            ack.Write(assistAccID);
            ack.Write(unk2);
            ack.Write(weaponID);
            ack.Write(victimAccID);
            ack.Write(unk3);
            ack.Write((byte)0x00);
            room.Broadcast(ack);
        }

        private void HandleScoreDefense(TcpSession session, Packet p)
        {
            var murderAccID = p.ReadUInt64();
            var unk1 = p.ReadUInt64(); // not sure
            var weaponID = p.ReadUInt32();
            var victimAccID = p.ReadUInt64();
            var unk3 = p.ReadUInt64(); // not sure

            //_logger.Debug("-C_SCORE_DEFENSE_REQ- Murder: {0} Target: {1} Weapon: {2}", murderAccID, victimAccID, weaponID);

            var murder = Players.GetPlayerByID(murderAccID);
            var victim = Players.GetPlayerByID(victimAccID);
            if (murder == null || victim == null || murder.Room == null || victim.Room == null)
                return;
            if (murder.Room.TunnelID != victim.Room.TunnelID)
                return;

            var room = murder.Room;
            if (room.TDWaiting || room.TimeState == EGameTimeState.HalfTime)
                return;
            if (room.GameRule != EGameRule.Touchdown)
                return;

            var tdGame = (TDGameScore)murder.GameScore;
            tdGame.TotalPoints += 4;
            tdGame.Defense++;

            var ack = new Packet(EGamePacket.SScoreDefenseAck);
            ack.Write((uint)0); // unk
            ack.Write(murderAccID);
            ack.Write(unk1);
            ack.Write(weaponID);
            ack.Write(victimAccID);
            ack.Write(unk3);
            ack.Write((byte)0x00);
            room.Broadcast(ack);
        }

        private void HandleScoreDefenseAssist(TcpSession session, Packet p)
        {
            var murderAccID = p.ReadUInt64();
            var unk1 = p.ReadUInt64(); // not sure
            var assistAccID = p.ReadUInt64();
            var unk2 = p.ReadUInt64(); // not sure
            var weaponID = p.ReadUInt32();
            var victimAccID = p.ReadUInt64();
            var unk3 = p.ReadUInt64(); // not sure

            //_logger.Debug("-C_SCORE_DEFENSE_ASSIST_REQ- Murder: {0} Assist: {1} Target: {2} Weapon: {3}", murderAccID, assistAccID, victimAccID, weaponID);

            var murder = Players.GetPlayerByID(murderAccID);
            var assist = Players.GetPlayerByID(assistAccID);
            var victim = Players.GetPlayerByID(victimAccID);
            if (murder == null || victim == null || murder.Room == null || victim.Room == null || assist == null || assist.Room == null)
                return;
            if (murder.Room.TunnelID != victim.Room.TunnelID || murder.Room.TunnelID != assist.Room.TunnelID)
                return;

            var room = murder.Room;
            if (room.TDWaiting || room.TimeState == EGameTimeState.HalfTime)
                return;
            if (room.GameRule != EGameRule.Touchdown)
                return;

            var tdGame = (TDGameScore)murder.GameScore;
            tdGame.TotalPoints += 4;
            tdGame.Offense++;

            tdGame = (TDGameScore)assist.GameScore;
            tdGame.TotalPoints += 2;
            tdGame.OffenseAssists++;

            var ack = new Packet(EGamePacket.SScoreDefenseAssistAck);
            ack.Write(murderAccID);
            ack.Write(unk1);
            ack.Write(assistAccID);
            ack.Write(unk2);
            ack.Write(weaponID);
            ack.Write(victimAccID);
            ack.Write(unk3);
            ack.Write((byte)0x00);
            room.Broadcast(ack);
        }

        private void HandleReboundFumbi(TcpSession session, Packet p)
        {
            var newAccID = p.ReadUInt64();
            var oldAccID = p.ReadUInt64();
            //_logger.Debug("-C_REBOUND_FUMBI_REQ- New ID: {0} Old ID: {1}", newAccID, oldAccID);

            var ack = new Packet(EGamePacket.SReboundFumbiAck);

            Player sender;
            if (!Players.TryGetValue(session.Guid, out sender))
            {
                session.StopListening();
                return;
            }
            if (sender.Room == null)
                return;
            var room = sender.Room;
            if (room.GameRule != EGameRule.Touchdown)
                return;

            var newPlr = Players.GetPlayerByID(newAccID);
            var oldPlr = Players.GetPlayerByID(oldAccID);
            if (newPlr == null || newPlr.Room == null || room.TDWaiting || sender.Room.TunnelID != newPlr.Room.TunnelID)
                return;
            if (oldAccID != 0 && (oldPlr == null || oldPlr.Room == null || oldPlr.Room.TunnelID != sender.Room.TunnelID))
                return;

            newPlr.GameScore.TotalPoints += 2;
            if (oldAccID != 0 && oldPlr.Team == ETeam.Alpha)
            {
                room.LastAlphaFumbi = DateTime.Now;
                room.LastAlphaFumbiID = oldAccID;
            }
            else if (oldAccID != 0 && oldPlr.Team == ETeam.Beta)
            {
                room.LastBetaFumbi = DateTime.Now;
                room.LastBetaFumbiID = oldAccID;
            }

            ack.Write(newAccID);
            ack.Write(oldAccID);
            room.Broadcast(ack);
        }

        private void HandleSuicide(TcpSession session, Packet p)
        {
            var accID = p.ReadUInt64();
            var unk1 = p.ReadUInt64(); // not sure
            var unk2 = p.ReadUInt32(); // weapon id?? makes no sense for suicide oO

            //_logger.Debug("-C_SCORE_SUICIDE_REQ- ID: {0} Unk1: {1} Unk2: {2}", accID, unk1, unk2);

            var player = Players.GetPlayerByID(accID);
            if (player == null || player.Room == null)
                return;

            var room = player.Room;
            if (room.TDWaiting || room.TimeState == EGameTimeState.HalfTime)
                return;

            if (room.GameRule == EGameRule.Deathmatch)
                ((DMGameScore)player.GameScore).Deaths++;

            var ack = new Packet(EGamePacket.SScoreSuicideAck);
            ack.Write(accID);
            ack.Write(unk1);
            ack.Write(unk2);
            room.Broadcast(ack);
        }

        async private void HandleTouchdown(TcpSession session, Packet p)
        {
            var accID = p.ReadUInt64();
            //_logger.Debug("-SC_TOUCHDOWN- ID: {0}", accID);

            var player = Players.GetPlayerByID(accID);
            if (player == null)
                return;

            Player sender;
            if (!Players.TryGetValue(session.Guid, out sender))
            {
                session.StopListening();
                return;
            }

            if (sender.Room == null || player.Room == null || sender.Room.TunnelID != player.Room.TunnelID || sender.Room.GameRule != EGameRule.Touchdown)
                return;
            var room = sender.Room;
            var team = player.Team;

            Player assist = null;
            switch (team)
            {
                case ETeam.Alpha:
                {
                    room.LastAlphaTD = DateTime.Now;
                    var ts = DateTime.Now - room.LastAlphaFumbi;
                    if (ts.TotalSeconds < 10) // 10 seconds timer for td assist?
                        assist = room.Players.GetPlayerByID(room.LastAlphaFumbiID);
                }
                break;

                case ETeam.Beta:
                {
                    room.LastBetaTD = DateTime.Now;
                    var ts = DateTime.Now - room.LastBetaFumbi;
                    if (ts.TotalSeconds < 10) // 10 seconds timer for td assist?
                        assist = room.Players.GetPlayerByID(room.LastBetaFumbiID);
                }
                break;
            }

            var tdGame = (TDGameScore)player.GameScore;
            tdGame.TotalPoints += 10;
            tdGame.TDScore++;

            var ack = new Packet(EGamePacket.SCTouchdown);
            ack.Write((byte)0x00); // unk | 0 - 3
            room.Broadcast(ack);

            if (assist == null)
            {
                ack = new Packet(EGamePacket.SScoreTouchdownAck);
                ack.Write(accID);
                room.Broadcast(ack);
            }
            else
            {
                tdGame = (TDGameScore)assist.GameScore;
                tdGame.TotalPoints += 5;
                tdGame.TDAssists++;

                ack = new Packet(EGamePacket.SScoreTouchdownAssistAck);
                ack.Write((uint)0); // unk
                ack.Write(player.AccountID);
                ack.Write(assist.AccountID);
                room.Broadcast(ack);
            }

            room.TDWaiting = true;
            if (player.Team == ETeam.Alpha)
            {
                room.ScoreAlpha++;
                room.BroadcastEventMessage(EPlayerEventMessage.TouchdownAlpha);
            }
            else
            {
                room.ScoreBeta++;
                room.BroadcastEventMessage(EPlayerEventMessage.TouchdownBeta);
            }

            await Task.Delay(10000); // wait 10 seconds after touchdown
            room.TDWaiting = false;
            if (room.TimeState == EGameTimeState.HalfTime || room.State != EGameRuleState.Playing)
                return;
            room.BroadcastEventMessage(EPlayerEventMessage.ResetRound);
        }

        private void HandleRoomKick(TcpSession session, Packet p)
        {
            var accID = p.ReadUInt64();
            //_logger.Debug("-C_ROOM_KICK_REQ- ID: {0}", accID);

            Player master;
            if (!Players.TryGetValue(session.Guid, out master))
            {
                session.StopListening();
                return;
            }
            var target = Players.GetPlayerByID(accID);

            if (master.Room == null || target.Room == null || master.Room.TunnelID != target.Room.TunnelID)
                return;

            var room = master.Room;
            if (master.AccountID != room.MasterID || master.State != EPlayerState.Lobby) // kick hack...
                return;

            room.Leave(target, 1);
        }

        private void HandleChangeTeam(TcpSession session, Packet p)
        {
            var toTeam = (ETeam)p.ReadByte();
            var gameMode = (EPlayerGameMode) p.ReadByte();
            //_logger.Debug("-C_CHANGE_TEAM_REQ- ToTeam: {0}", toTeam.ToString());

            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            var room = plr.Room;
            if (room == null)
                return;

            var ack = new Packet(EGamePacket.SChangeTeamAck);
            switch (toTeam)
            {
                case ETeam.Alpha:
                {
                    var numAlpha = room.CountInTeam(ETeam.Alpha, plr.GameMode);
                    var limit = plr.GameMode == EPlayerGameMode.Normal ? room.PlayerLimit/2 : room.SpectatorLimit/2;
                    if (numAlpha >= limit) // full
                        return;

                    plr.Team = ETeam.Alpha;

                    ack.Write(plr.AccountID);
                    ack.Write((byte)toTeam);
                    ack.Write((byte)plr.GameMode);
                    room.Broadcast(ack);
                }
                    break;
                case ETeam.Beta:
                {
                    var numBeta = room.CountInTeam(ETeam.Beta, plr.GameMode);
                    var limit = plr.GameMode == EPlayerGameMode.Normal ? room.PlayerLimit / 2 : room.SpectatorLimit / 2;
                    if (numBeta >= limit) // full
                        return;

                    plr.Team = ETeam.Beta;

                    ack.Write(plr.AccountID);
                    ack.Write((byte)toTeam);
                    ack.Write((byte)plr.GameMode);
                    room.Broadcast(ack);
                }
                break;
            }
        }

        private void HandleRandomShop(TcpSession session, Packet p)
        {
            var category = p.ReadByte(); // weapon = 1 - costume = 0
            var typeID = p.ReadByte(); // weapon = 2 - costume = 1
            //_logger.Debug("-C_RANDOMSHOP_REQ- Category: {0} TypeID: {1}", category, typeID);

            var ack = new Packet(EGamePacket.SResultAck);
            ack.Write((uint)EServerResult.FailedToRequestTask);
            session.Send(ack);
            return;

            // TODO
            ack = new Packet(EGamePacket.SRandomshopItemInfoAck);
            ack.Write((byte)0);
            ack.Write(category); // category
            ack.Write((byte)80); // effect
            ack.Write((uint)1001); // item id
            ack.Write((uint)66125826); // skin id?
            ack.Write((uint)0);
            session.Send(ack);

            ack = new Packet(EGamePacket.SRandomshopChanceInfoAck);
            ack.Write((uint)10000);
            session.Send(ack);
        }

        private void HandleGetPlayerInfo(TcpSession session, Packet p)
        {
            ulong accID = p.ReadUInt32();
            //_logger.Debug("-C_GET_PLAYER_INFO_REQ- ID: {0}", accID);

            Player player;
            if (!Players.TryGetValue(session.Guid, out player))
            {
                session.StopListening();
                return;
            }

            //var ack = new Packet(0x3A);
            //ack.Write(player.AccountID);
            //ack.Write((ulong)0);
            //ack.Write((ulong)0);
            //ack.WriteStringBuffer("STRING1", 7);
            //ack.WriteStringBuffer("NOOB", 4);
            //session.Send(ack);
        }

        private void HandleLogout(TcpSession session, Packet p)
        {
            //_logger.Debug("-C_LOGOUT_REQ-");

            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            Players.TryRemove(session.Guid, out plr);

            var ack = new Packet(EGamePacket.SLogoutAck);
            session.Send(ack);
        }

        private void HandleChangePlayerGameMode(TcpSession session, Packet p)
        {
            var gameMode = (EPlayerGameMode)p.ReadByte();
            //_logger.Debug("-C_ROOM_PLAYERGAMEMODE_CHANGE_REQ- GameMode: {0}", gameMode.ToString());

            Player player;
            if (!Players.TryGetValue(session.Guid, out player))
            {
                session.StopListening();
                return;
            }
            if(player.Room == null)
                return;
            var room = player.Room;

            player.GameMode = gameMode;

            var ack = new Packet(EGamePacket.SChangeTeamAck);
            ack.Write(player.AccountID);
            ack.Write((byte)player.Team);
            ack.Write((byte)player.GameMode);
            room.Broadcast(ack);

            room.BroadcastBriefing();
        }

        private void HandleRoomChangeItems(TcpSession session, Packet p)
        {
            var accID = p.ReadUInt64();
            var skill = p.ReadUInt32();
            var skill2 = p.ReadUInt32(); // alpha thing -> removed in beta
            var weapon1 = p.ReadUInt32();
            var weapon2 = p.ReadUInt32();
            var weapon3 = p.ReadUInt32();
            var unk = p.ReadBytes(27);

            //_logger.Debug("-C_ROOM_CHANGE_ITEMS_REQ-");

            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            var room = plr.Room;
            if (room == null)
                return;

            // Slot freeze bug fix
            if (skill2 != 0)
                skill = skill2;

            var ack = new Packet(EGamePacket.SRoomChangeItemsAck);
            ack.Write(accID);
            ack.Write(skill);
            ack.Write(0);
            ack.Write(weapon1);
            ack.Write(weapon2);
            ack.Write(weapon3);
            ack.Write(unk);
            room.Broadcast(ack, plr.AccountID);
        }

        private void HandleChangeRoom(TcpSession session, Packet p)
        {
            //_logger.Debug("-C_CHANGE_ROOM_REQ-");
            // TODO
            var ack = new Packet(EGamePacket.SResultAck);
            ack.Write((uint)EServerResult.FailedToRequestTask);
            session.Send(ack);
        }

        private void HandleTutorialCompleted(TcpSession session, Packet p)
        {
            var unk = p.ReadUInt32();

            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            if (plr.TutorialCompleted) return;

            plr.PEN += 5000;
            plr.TutorialCompleted = true;
            GameDatabase.Instance.UpdateTutorialFlag(plr);
            GameDatabase.Instance.UpdateMoney(plr);
        }

        private void HandleScoreSurvival(TcpSession session, Packet p)
        {
            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }

            var sGame = (SurvivalGameScore)plr.GameScore;
            sGame.TotalPoints++;
            sGame.Kills++;
        }

        private void HandleQuickJoin(TcpSession session, Packet p)
        {
            var ack = new Packet(EGamePacket.SResultAck);
            ack.Write((uint)EServerResult.FailedToRequestTask);
            session.Send(ack);
        }

        private void HandleMovePlayer(TcpSession session, Packet p)
        {
            var targetID = p.ReadUInt64();
            var unk = p.ReadUInt64();
            var fromTeam = (ETeam) p.ReadByte();
            var toTeam = (ETeam) p.ReadByte();

            Player plr;
            if (!Players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            if (plr.Room == null)
                return;
            if (plr.Room.State != EGameRuleState.Waiting)
                return;

            var targetPlr = plr.Room.Players.GetPlayerByID(targetID);
            if (targetPlr == null)
                return;

            var room = plr.Room;

            switch (toTeam)
            {
                case ETeam.Alpha:
                    {
                        var numAlpha = room.CountInTeam(ETeam.Alpha, targetPlr.GameMode);
                        var limit = targetPlr.GameMode == EPlayerGameMode.Normal ? room.PlayerLimit / 2 : room.SpectatorLimit / 2;
                        if (numAlpha >= limit) // full
                            return;
                        targetPlr.Team = ETeam.Alpha;
                    }
                    break;
                case ETeam.Beta:
                    {
                        var numBeta = room.CountInTeam(ETeam.Beta, targetPlr.GameMode);
                        var limit = targetPlr.GameMode == EPlayerGameMode.Normal ? room.PlayerLimit / 2 : room.SpectatorLimit / 2;
                        if (numBeta >= limit) // full
                            return;
                        targetPlr.Team = ETeam.Beta;
                    }
                    break;
            }

            var ack = new Packet(EGamePacket.SCRoomMovePlayer);
            ack.Write(targetID);
            ack.Write(unk);
            ack.Write((byte)fromTeam);
            ack.Write((byte)toTeam);
            session.Send(ack);
            room.BroadcastBriefing();
        }

        private void HandleShuffle(TcpSession session, Packet p)
        {
            var ack = new Packet(EGamePacket.SResultAck);
            ack.Write((uint)EServerResult.FailedToRequestTask);
            session.Send(ack);
        }

        private void SendRoomList(TcpSession session)
        {
            var ack = new Packet(EGamePacket.SRoomListAck);
            ack.Write((ushort)Rooms.Count);
            foreach (var room in Rooms.Values)
            {
                ack.Write(room.ID);
                ack.Write(room.GetConnectingCount()); //connecting people count
                ack.Write((byte)room.Players.Count);
                ack.Write((byte)room.State);
                ack.Write(room.GetPing()); // ping
                ack.Write(room.MatchKey);
                ack.WriteStringBuffer(room.Name, 31);
                ack.Write(room.PublicType); // has pw
                ack.Write(room.TimeLimit);
                ack.Write(room.ScoreLimit);
                ack.Write(room.IsFriendly);
                ack.Write(room.IsBalanced);
                ack.Write(room.MinLevel);
                ack.Write(room.MaxLevel);
                ack.Write(room.EquipLimit);
                ack.Write(room.IsNoIntrusion);
            }
            session.Send(ack);
        }

        private bool ValidateSession(uint sessionID, ulong accID, IPAddress ip)
        {
            var channel = _authRemoteClient.CreateChannel<IAuthRemote>("IAuthRemote");
            if (channel == null)
            {
                Console.WriteLine("channel is null");
                return false;
            }


            try
            {
                var res = channel.ValidateSession(sessionID, accID, ip);
                _authRemoteClient.CloseChannel(channel);
                return res;
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0}\n{1}", ex.Message, ex.StackTrace);
                _authRemoteClient.CloseChannel(channel);
                return false;
            }
        }

        public void BroadcastNotice(string msg)
        {
            var len = (ushort) (msg.Length + 1);
            var ack = new Packet(EGamePacket.SNoticeAck);
            ack.Write(len);
            ack.Write(msg);

            _server.Broadcast(ack);
        }
    }
}
