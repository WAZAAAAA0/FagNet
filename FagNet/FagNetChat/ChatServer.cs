using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using FagNet.Core.Constants;
using FagNet.Core.Constants.Packets;
using FagNet.Core.Cryptography;
using FagNet.Core.Data;
using FagNet.Core.Database;
using FagNet.Core.Network;
using FagNet.Core.Network.Events;
using FagNet.Core.Utils;

namespace FagNetChat
{
    class ChatServer
    {
        public static ChatServer Instance { get { return Singleton<ChatServer>.Instance; } }

        private readonly PacketLogger _packetLogger;
        private readonly Logger _logger;

        private readonly TcpServer _server;
        private readonly RemoteClient _authRemoteClient;

        private readonly PlayerCollection _players = new PlayerCollection();
        private ChannelCollection _channels = new ChannelCollection();

        public ChatServer()
        {
            _packetLogger = new PacketLogger();
            _logger = new Logger() { WriteToConsole = true };
            _logger.Load(Path.Combine("logs", string.Format("chat_{0}.log", DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss"))));
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Error(s, new ExceptionEventArgs((Exception)e.ExceptionObject));
                Environment.Exit(0);
            };

            _packetLogger.Load("chat_packets.log");

            _logger.Info("Loading chat_config.xml...");
            ChatConfig.Load();
            _logger.Info("Setting up servers...");
            _server = new TcpServer(IPAddress.Parse(ChatConfig.Instance.IP), ChatConfig.Instance.Port);
            _server.PacketReceived += HandlePacket;
            _server.ClientDisconnected += ClientDisconnected;
            _server.Error += Error;

            var isMono = Type.GetType("Mono.Runtime") != null;
            switch (ChatConfig.Instance.AuthRemote.Binding)
            {
                case "pipe":
                    if (isMono)
                    {
                        _logger.Error("pipe is not supported in mono, use http!");
                        Environment.Exit(1);
                        return;
                    }
                    _authRemoteClient = new RemoteClient(ERemoteBinding.Pipe, string.Format("localhost/FagNetAuth/{0}/", SHA256.ComputeHash(ChatConfig.Instance.AuthRemote.Password)));
                    break;

                case "tcp":
                    if (isMono)
                    {
                        _logger.Error("pipe is not supported in mono, use http!");
                        Environment.Exit(1);
                        return;
                    }
                    _authRemoteClient = new RemoteClient(ERemoteBinding.Pipe, string.Format("{0}:{1}/FagNetAuth/{2}/", ChatConfig.Instance.AuthRemote.Server, ChatConfig.Instance.AuthRemote.Port, SHA256.ComputeHash(ChatConfig.Instance.AuthRemote.Password)));
                    break;

                case "http":
                    _authRemoteClient = new RemoteClient(ERemoteBinding.Http, string.Format("{0}:{1}/FagNetAuth/{2}/", ChatConfig.Instance.AuthRemote.Server, ChatConfig.Instance.AuthRemote.Port, SHA256.ComputeHash(ChatConfig.Instance.AuthRemote.Password)));
                    break;

                default:
                    _logger.Error("Invalid remote binding '{0}' for AuthRemote", ChatConfig.Instance.AuthRemote.Binding);
                    Environment.Exit(1);
                    break;
            }
        }

        public void Start()
        {
            _logger.Info("Connecting to MySQL database...");
            try
            {
                GameDatabase.Instance.TryConnect(ChatConfig.Instance.MySQLGame.Server, ChatConfig.Instance.MySQLGame.User, ChatConfig.Instance.MySQLGame.Password, ChatConfig.Instance.MySQLGame.Database);
                AuthDatabase.Instance.TryConnect(ChatConfig.Instance.MySQLAuth.Server, ChatConfig.Instance.MySQLAuth.User, ChatConfig.Instance.MySQLAuth.Password, ChatConfig.Instance.MySQLAuth.Database);

                _channels = GameDatabase.Instance.GetChannels(EServerType.Chat);
            }
            catch (Exception ex)
            {
                _logger.Error("Could not connect to MySQL database: {0}\r\n{1}",
                    ex.Message, ex.InnerException);
                Environment.Exit(0);
            }
            _server.Start();
            _logger.Info("Ready for connections!");
        }

        public void Stop()
        {
            _logger.Info("Shutting down...");
            _server.Stop();
            _logger.Dispose();
            _packetLogger.Dispose();
        }

        private void HandlePacket(object sender, PacketReceivedEventArgs e)
        {
            //_packetLogger.Log<EChatPacket>(e.Packet);

            switch (e.Packet.PacketID)
            {
                case (byte)EChatPacket.CKeepAliveAck:
                    break;

                case (byte)EChatPacket.CLoginReq:
                    HandleLoginRequest(e.Session, e.Packet);
                    break;

                case (byte)EChatPacket.CMessageReq:
                    HandleChatMessage(e.Session, e.Packet);
                    break;

                case (byte)EChatPacket.CWhisperReq:
                    HandleWhisper(e.Session, e.Packet);
                    break;

                case (byte)EChatPacket.CChannelListReq:
                    HandleChannelListRequest(e.Session, e.Packet);
                    break;

                case (byte)EChatPacket.CChannelEnterReq:
                    HandleChannelEnter(e.Session, e.Packet);
                    break;

                case (byte)EChatPacket.CChannelLeaveReq:
                    HandleChannelLeave(e.Session, e.Packet);
                    break;

                case (byte)EChatPacket.CFriendListReq:
                    HandleFriendListRequest(e.Session, e.Packet);
                    break;

                case (byte)EChatPacket.CCombiListReq:
                    HandleCombiListRequest(e.Session, e.Packet);
                    break;

                case (byte)EChatPacket.CDenyListReq:
                    HandleDenyListRequest(e.Session, e.Packet);
                    break;

                case (byte)EChatPacket.CGetDataReq:
                    HandleGetData(e.Session, e.Packet);
                    break;

                case (byte)EChatPacket.CSetDataReq:
                    HandleSetDataRequest(e.Session, e.Packet);
                    break;

                case (byte)EChatPacket.CSetStateReq:
                    //_logger.Debug("-C_SET_STATE_REQ-");
                    break;

                case (byte)EChatPacket.CAddDenyReq:
                    HandleAddDeny(e.Session, e.Packet);
                    break;

                case (byte)EChatPacket.CRemoveDenyReq:
                    HandleRemoveDeny(e.Session, e.Packet);
                    break;

                case (byte)EChatPacket.CAddFriendReq:
                    HandleAddFriend(e.Session, e.Packet);
                    break;

                case (byte)EChatPacket.CBRSFriendNotifyReq:
                    HandleBRSFriendNotify(e.Session, e.Packet);
                    break;

                default:
                    _logger.Warning("Unkown packet {0}", e.Packet.PacketID.ToString("x2"));
                    break;
            }
        }

        private void ClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            Player plr;
            if (!_players.TryRemove(e.Session.Guid, out plr))
                return;

            if (plr.Channel != null)
                plr.Channel.Leave(plr);
        }

        private void Error(object sender, ExceptionEventArgs e)
        {
            _logger.Error(string.Format("{0}\r\n{1}", e.Exception.Message, e.Exception.StackTrace));
            if (e.Exception.InnerException != null)
                _logger.Error(string.Format("{0}\r\n{1}", e.Exception.InnerException.Message, e.Exception.InnerException.StackTrace));
        }

        private void HandleLoginRequest(TcpSession session, Packet p)
        {
            var ip = session.Client.Client.RemoteEndPoint as IPEndPoint;
            var accID = p.ReadUInt64();
            var nickname = p.ReadCString();
            _logger.Info("-CLoginReq- User: {0} ID: {1}", nickname, accID);

            var ack = new Packet(EChatPacket.SLoginAck);
            if (accID == 0 || !ValidateSession(accID, nickname, ip.Address) || _players.GetPlayerByID(accID) != null /* prevent multiple logins! */)
            {
                ack.Write((uint)1); // error code
                session.Send(ack);
                session.StopListening();
                return;
            }

            var plr = GameDatabase.Instance.GetPlayer(accID);
            plr.Session = session;
            _players.TryAdd(session.Guid, plr);

            ack.Write((uint)0); // error code
            session.Send(ack);
        }

        private void HandleChatMessage(TcpSession session, Packet p)
        {
            var channelID = p.ReadUInt32();
            var msgSize = p.ReadUInt16();
            var msg = Encoding.ASCII.GetString(p.ReadBytes(msgSize));
            //_logger.Debug("-C_MESSAGE_REQ- ChannelID: {0} Msg: {1}", channelID, msg);

            Player plr;
            if (!_players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            if (plr.Channel == null)
                return;

            var ack = new Packet(EChatPacket.SMessageAck);
            ack.Write(plr.AccountID);
            ack.Write((uint)plr.Channel.ID);
            ack.Write((ushort)msg.Length);
            ack.Write(msg);
            plr.Channel.Broadcast(ack);
        }

        private void HandleWhisper(TcpSession session, Packet p)
        {
            var accID = p.ReadUInt64();
            var unk1 = p.ReadUInt32();
            var unk2 = p.ReadByte();
            var msgSize = p.ReadUInt16();
            var msg = Encoding.ASCII.GetString(p.ReadBytes(msgSize));
            _logger.Debug("-C_WHISPER_REQ- accID: {0} unk1: {1} unk2: {2} Size: {3} Msg: {4}", accID, unk1, unk2, msgSize, msg);

            var target = _players.GetPlayerByID(accID);
            Player plr;
            if (!_players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            if (target == null)
                return;

            var ack = new Packet(EChatPacket.SWhisperAck);
            ack.Write(plr.AccountID);
            ack.Write(unk1);
            ack.Write(unk2);
            ack.Write(msgSize);
            ack.Write(msg);
            target.Session.Send(ack);
        }

        private void HandleChannelListRequest(TcpSession session, Packet p)
        {
            //_logger.Debug("-C_CHANLIST_REQ-");

            var ack = new Packet(EChatPacket.SChannelListAck);
            ack.Write((uint)_channels.Count);
            foreach (var chan in _channels.Values)
            {
                ack.Write((byte)chan.ID); // unk
                ack.Write((uint)chan.ID); // id??
                ack.WriteStringBuffer(chan.Name, 21, Encoding.ASCII);
            }
            session.Send(ack);
        }

        private void HandleFriendListRequest(TcpSession session, Packet p)
        {
            //_logger.Debug("-C_FRIENDLIST_REQ-");
            Player plr;
            if (!_players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }

            SendFriendList(plr);
        }

        private void HandleCombiListRequest(TcpSession session, Packet p)
        {
            //_logger.Debug("-C_COMBILIST_REQ-");

            Player plr;
            if (!_players.TryGetValue(session.Guid, out plr)) return;
            var ack = new Packet(EChatPacket.SCombiListAck);
            ack.Write(plr.AccountID);
            ack.Write((uint)0);
            session.Send(ack);
        }

        private void HandleDenyListRequest(TcpSession session, Packet p)
        {
            //_logger.Debug("-C_DENYLIST_REQ-");
            Player plr;
            if (!_players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }

            SendDenyList(plr);
        }

        private void HandleSetDataRequest(TcpSession session, Packet p)
        {
            //_logger.Debug("-C_SETDATA_REQ-");

            Player plr;
            if (!_players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }

            p.ReadUInt16(); // unk
            p.ReadUInt64(); // accID
            plr.ServerID = p.ReadUInt16();
            var channelID = p.ReadInt16();
            var roomID = p.ReadInt32();

            if(roomID == -1)
                roomID = 0;
            if (channelID == -1)
                channelID = 0;

            plr.Room = new Room(null, EServerType.Chat) { ID = (uint)roomID };
            plr.CommunityByte = p.ReadByte();
            p.ReadUInt32(); // total exp
            p.ReadBytes(32); // td/dm info

            plr.AllowCombiRequest = (EAllowCommunityRequest)p.ReadByte();
            plr.AllowFriendRequest = (EAllowCommunityRequest)p.ReadByte();
            plr.AllowInvite = (EAllowCommunityRequest)p.ReadByte();
            plr.AllowInfoRequest = (EAllowCommunityRequest)p.ReadByte();

            plr.CommunityData = p.ReadBytes(41);

            Channel channel;
            if (!_channels.TryGetValue((ushort)channelID, out channel))
                return;
            if (plr.Channel == null && channelID > 0) // join
            {
                var ack = new Packet(EChatPacket.SChannelPlayerListInfoAck);
                ack.Write((uint)channel.ID);
                ack.Write(channel.Players.Count);
                foreach (var player in channel.Players.Values)
                    ack.WriteChatUserData(player);

                session.Send(ack);
                channel.Join(plr);
            }
            else if(channelID == 0) // leave
            {
                if(plr.Channel != null)
                    plr.Channel.Leave(plr);
            }
            else // update
            {
                var ack = new Packet(EChatPacket.SChannelPlayerListInfoAck);
                ack.Write((uint)channel.ID);
                ack.Write(channel.Players.Count);
                foreach (var player in channel.Players.Values)
                    ack.WriteChatUserData(player);

                channel.Broadcast(ack);
            }
        }

        private void HandleChannelEnter(TcpSession session, Packet p)
        {
            var chanName = p.ReadCString();
            //_logger.Debug("-C_CHANENTER_REQ- Name: {0}", chanName);

            Player plr;
            if (!_players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            var chan = _channels.GetChannelByName(chanName);
            if (chan.ID == 0)
                return;

            var ack = new Packet(EChatPacket.SChannelEnterAck);
            ack.Write((uint)chan.ID);
            session.Send(ack);
        }

        private void HandleChannelLeave(TcpSession session, Packet p)
        {
            //_logger.Debug("-C_CHAN_LEAVE_REQ-");
            Player plr;
            if (_players.TryGetValue(session.Guid, out plr)) return;
            session.StopListening();
        }

        private void HandleGetData(TcpSession session, Packet p)
        {
            var accID = p.ReadUInt64();
            //_logger.Debug("-C_GET_DATA_REQ- ID: {0}", accID);

            var ack = new Packet(EChatPacket.SGetDataAck);
            ack.Write(accID); // accid

            Player sender;
            if (!_players.TryGetValue(session.Guid, out sender))
            {
                session.StopListening();
                return;
            }

            var plr = _players.GetPlayerByID(accID);
            if(plr == null)
            {
                ack.Write((byte)0x01);
                session.Send(ack);
                return;
            }

            ack.Write((byte)0x00); // result code
            ack.WriteChatUserData(plr, true);
            session.Send(ack);
        }

        private void HandleAddDeny(TcpSession session, Packet p)
        {
            var accID = p.ReadUInt64();
            var nickname = p.ReadCStringBuffer(31);
            //_logger.Debug("-C_ADD_DENY_REQ- ID: {0} Nickname: {1}", accID, nickname);

            Player plr;
            if (!_players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }

            var ack = new Packet(EChatPacket.SAddDenyAck);
            if (!plr.DenyList.ContainsKey(accID))
            {
                plr.DenyList.Add(accID, nickname);
                GameDatabase.Instance.AddDeny(plr.AccountID, accID, nickname);
                ack.Write((byte)EDenyResult.OK);
            }
            else
            {
                ack.Write((byte)EDenyResult.Failed2);
            }

            ack.Write(accID);
            ack.WriteStringBuffer(nickname, 31);
            session.Send(ack);

            SendDenyList(plr);
        }

        private void HandleRemoveDeny(TcpSession session, Packet p)
        {
            var accID = p.ReadUInt64();
            var nickname = p.ReadCStringBuffer(31);
            //_logger.Debug("-C_REMOVE_DENY_REQ- ID: {0} Nickname: {1}", accID, nickname);

            Player plr;
            if (!_players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }

            var ack = new Packet(EChatPacket.SRemoveDenyAck);
            if (plr.DenyList.ContainsKey(accID))
            {
                plr.DenyList.Remove(accID);
                GameDatabase.Instance.RemoveDeny(plr.AccountID, accID);
                ack.Write((byte)EDenyResult.OK);
            }
            else
            {
                ack.Write((byte)EDenyResult.Failed2);
            }

            ack.Write(accID);
            ack.WriteStringBuffer(nickname, 31);
            session.Send(ack);

            SendDenyList(plr);
        }

        private void HandleAddFriend(TcpSession session, Packet p)
        {
            var accID = p.ReadUInt64();
            var nickname = p.ReadCStringBuffer(31);
            //_logger.Debug("-C_ADD_FRIEND_REQ- ID: {0} Nickname: {1}", accID, nickname);

            Player plr;
            if (!_players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }

            var ack = new Packet(EChatPacket.SAddFriendAck);
            ack.Write(accID);

            var plrToRequest = _players.GetPlayerByID(accID);
            if (plrToRequest == null)
            {
                ack.Write((byte)EAddFriendResult.DoenstExist);
            }
            else
            {
                var friend = plr.FriendList.FirstOrDefault(f => f.ID == accID);
                if (friend == null)
                {
                    plr.FriendList.Add(new Friend() { ID = accID, Nickname = nickname, Accepted = false });
                    GameDatabase.Instance.AddFriend(plr.AccountID, accID, nickname);

                    SendBRSFriendNotify(plr, plrToRequest, EFriendNotify.Request);

                    ack.Write((byte)EAddFriendResult.MadeRequest);
                }
                else
                {
                    if (friend.Accepted)
                        ack.Write((byte)EAddFriendResult.AlreadyInList);
                    else
                        ack.Write((byte)EAddFriendResult.AlreadyRequested);
                }
            }

            ack.WriteStringBuffer(nickname, 31);
            session.Send(ack);
        }

        private void HandleBRSFriendNotify(TcpSession session, Packet p)
        {
            var accID = p.ReadUInt64();
            var accepted = p.ReadInt32() > 0;
            var nickname = p.ReadCStringBuffer(31);
            //_logger.Debug("-C_ADD_FRIEND_REQ- ID: {0} Nickname: {1}", accID, nickname);

            Player plr;
            if (!_players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            Packet ack;

            var plrFromRequest = _players.GetPlayerByID(accID);
            if (plrFromRequest == null) return;

            if (accepted)
            {
                SendBRSFriendNotify(plr, plrFromRequest, EFriendNotify.Accepted);
                SendBRSFriendNotify(plrFromRequest, plr, EFriendNotify.Accepted);
            }
            else
            {
                SendBRSFriendNotify(plr, plrFromRequest, EFriendNotify.Denied);
                SendBRSFriendNotify(plr, plrFromRequest, EFriendNotify.DeleteRelation);

                SendBRSFriendNotify(plrFromRequest, plr, EFriendNotify.DeleteRelation);
            }


            var friend = plrFromRequest.FriendList.FirstOrDefault(f => f.ID == plr.AccountID);
            if (friend == null) return;
            if (accepted)
            {
                friend.Accepted = true;
                GameDatabase.Instance.UpdateFriend(plrFromRequest.AccountID, friend.ID, friend.Accepted);

                var newFriend = new Friend()
                {
                    ID = plrFromRequest.AccountID,
                    Nickname = plrFromRequest.Nickname,
                    Accepted = true
                };
                plr.FriendList.Add(newFriend);
                GameDatabase.Instance.AddFriend(plr.AccountID, newFriend.ID, newFriend.Nickname, newFriend.Accepted);
            }
            else
            {
                plrFromRequest.FriendList.Remove(friend);
                GameDatabase.Instance.RemoveFriend(plrFromRequest.AccountID, friend.ID);
            }
        }

        private static void SendDenyList(Player plr)
        {
            var ack = new Packet(EChatPacket.SDenyListAck);
            ack.Write((uint)plr.DenyList.Count);
            foreach (var entry in plr.DenyList)
            {
                ack.Write(entry.Key);
                ack.WriteStringBuffer(entry.Value, 31);
            }
            plr.Session.Send(ack);
        }

        private static void SendFriendList(Player plr)
        {
            var ack = new Packet(EChatPacket.SFriendListAck);
            ack.Write((uint)plr.FriendList.Count);
            foreach (var entry in plr.FriendList)
            {
                ack.Write(entry.ID);
                ack.Write((uint)2); // unk
                ack.Write((byte)2); // unk2 (89, 72, 73)
                //ack.Write((byte)0); // unk3
                ack.Write(entry.Nickname);
            }
            plr.Session.Send(ack);
        }

        private static void SendBRSFriendNotify(Player from, Player to, EFriendNotify mode)
        {
            var ack = new Packet(EChatPacket.SBRSFriendNotifyAck);
            ack.Write(from.AccountID);
            ack.Write(to.AccountID);
            ack.Write((byte)mode);
            ack.Write(from.Nickname);
            to.Session.Send(ack);
        }

        private bool ValidateSession(ulong accID, string nickname, IPAddress ip)
        {
            var channel = _authRemoteClient.CreateChannel<IAuthRemote>("IAuthRemote");
            if (channel == null)
                return false;

            try
            {
                var res = channel.ValidateSession(nickname, accID, ip);
                _authRemoteClient.CloseChannel(channel);
                return res;
            }
            catch (Exception)
            {
                _authRemoteClient.CloseChannel(channel);
                return false;
            }
        }
    }
}
