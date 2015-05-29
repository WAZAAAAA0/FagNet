using System;
using System.IO;
using System.Linq;
using System.Net;
using FagNet.Core.Constants;
using FagNet.Core.Constants.Packets;
using FagNet.Core.Data;
using FagNet.Core.Database;
using FagNet.Core.Network;
using FagNet.Core.Network.Events;
using FagNet.Core.Utils;

namespace FagNetRelay
{
    class RelayServer
    {
        public static RelayServer Instance { get { return Singleton<RelayServer>.Instance; } }

        private readonly PacketLogger _packetLogger;
        private readonly Logger _logger;

        private readonly TcpServer _server;
        private readonly PlayerCollection _players = new PlayerCollection();
        private readonly RoomCollection _rooms = new RoomCollection();

        public RelayServer()
        {
            _packetLogger = new PacketLogger();
            _logger = new Logger() { WriteToConsole = true };
            _logger.Load(Path.Combine("logs", string.Format("relay_{0}.log", DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss"))));

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Error(s, new ExceptionEventArgs((Exception)e.ExceptionObject));
                Environment.Exit(0);
            };
            _packetLogger.Load("relay_packets.log");

            _logger.Info("Loading relay_config.xml...");
            RelayConfig.Load();
            _logger.Info("Setting up servers...");
            _server = new TcpServer(IPAddress.Parse(RelayConfig.Instance.IP), RelayConfig.Instance.Port);
            _server.PacketReceived += HandlePacket;
            _server.ClientDisconnected += ClientDisconnected;
            _server.Error += Error;
        }

        public void Start()
        {
            _logger.Info("Connecting to MySQL database...");
            try
            {
                GameDatabase.Instance.TryConnect(RelayConfig.Instance.MySQLAuth.Server, RelayConfig.Instance.MySQLAuth.User, RelayConfig.Instance.MySQLAuth.Password, RelayConfig.Instance.MySQLAuth.Database);
                AuthDatabase.Instance.TryConnect(RelayConfig.Instance.MySQLGame.Server, RelayConfig.Instance.MySQLGame.User, RelayConfig.Instance.MySQLGame.Password, RelayConfig.Instance.MySQLGame.Database);
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
            //_packetLogger.Log<ERelayPacket>(e.Packet);
            switch (e.Packet.PacketID)
            {
                case (byte)ERelayPacket.CLoginReq:
                    HandleLoginRequest(e.Session, e.Packet);
                    break;

                case (byte)ERelayPacket.CJoinTunnelReq:
                    HandleJoinTunnelRequest(e.Session, e.Packet);
                    break;

                case (byte)ERelayPacket.CLeaveTunnelReq:
                    HandleLeaveTunnelRequest(e.Session, e.Packet);
                    break;

                case (byte)ERelayPacket.CUseTunnelReq:
                    HandleUseTunnelRequest(e.Session, e.Packet);
                    break;

                case (byte)ERelayPacket.CDetourPacketReq:
                    HandleDetourPacketRequest(e.Session, e.Packet);
                    break;

                case (byte)ERelayPacket.CKeepAliveReq:
                    break;

                default:
                    _logger.Warning("Unkown packet {0}", e.Packet.PacketID.ToString("x2"));
                    break;
            }
        }

        private void ClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            Player plr;
            _players.TryRemove(e.Session.Guid, out plr);
            if (plr == null || plr.Room == null)
                return;

            var room = plr.Room;
            room.Leave(plr);
            if (room.Players.Count == 0)
                _rooms.TryRemove(room.TunnelID, out room);
        }

        private void Error(object sender, ExceptionEventArgs e)
        {
            _logger.Error(string.Format("{0}\r\n{1}", e.Exception.Message, e.Exception.StackTrace));
            if (e.Exception.InnerException != null)
                _logger.Error(string.Format("{0}\r\n{1}", e.Exception.InnerException.Message, e.Exception.InnerException.StackTrace));
        }

        private void HandleLoginRequest(TcpSession session, Packet p)
        {
            var plr = new Player();
            plr.Session = session;
            plr.Nickname = p.ReadCString();
            plr.AccountID = AuthDatabase.Instance.GetAccountIDByNickname(plr.Nickname);
            _logger.Info("-CLoginReq- Nickname: {0}", plr.Nickname);

            var ack = new Packet(ERelayPacket.SResultAck);
            if (_players.GetPlayerByID(plr.AccountID) != null /* prevent multiple logins! */ || !_players.TryAdd(session.Guid, plr))
            {
                ack.Write((uint)1);
                session.Send(ack);
                return;
            }
            ack.Write((uint)0); // error code
            session.Send(ack);
        }

        private void HandleJoinTunnelRequest(TcpSession session, Packet p)
        {
            var tunnelID = p.ReadUInt32();
            var slotID = p.ReadByte();
            //_logger.Debug("-C_JOIN_TUNNEL_REQ- Slot: {0} Tunnel: {1}", slotID, tunnelID);

            Player plr;
            if (!_players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }

            Room room;
            if (!_rooms.TryGetValue(tunnelID, out room))
            {
                room = new Room(_rooms, EServerType.Relay) {TunnelID = tunnelID};
                _rooms.TryAdd(tunnelID, room);
            }

            room.Join(plr);
            plr.SlotID = slotID;

            var ack = new Packet(ERelayPacket.SResultAck);
            ack.Write((uint)3); // error code
            session.Send(ack);
        }

        private void HandleLeaveTunnelRequest(TcpSession session, Packet p)
        {
            //_logger.Debug("-C_LEAVE_TUNNEL_REQ-");

            Player plr;
            if (!_players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            var room = plr.Room;
            if (room == null)
                return;

            var ls = room.Players;
            var slotID = plr.SlotID;

            room.Leave(plr);
            plr.RelayProxies.Clear();

            foreach (var entry in ls.Values.Where(entry => entry.AccountID != plr.AccountID).Where(entry => entry.RelayProxies.Contains(slotID)))
            {
                entry.RelayProxies.Remove(slotID);
            }

            var ack = new Packet(ERelayPacket.SResultAck);
            ack.Write((uint)6); // error code
            session.Send(ack);
        }

        private void HandleUseTunnelRequest(TcpSession session, Packet p)
        {
            var slotID = p.ReadByte();
            if (slotID == 1)
                return;

            Player plr;
            if (!_players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            var target = _players.GetPlayerBySlot(slotID);
            if (target == null)
                return;

            if (!plr.RelayProxies.Contains(target.SlotID))
                plr.RelayProxies.Add(target.SlotID);

            //_logger.Debug("-C_USE_TUNNEL_REQ- TargetSlot: {0} User: {1} Slot: {2}", slotID, plr.Nickname, plr.SlotID);

            var ack = new Packet(ERelayPacket.SUseTunnelAck);
            ack.Write(slotID);
            session.Send(ack);
        }

        private void HandleDetourPacketRequest(TcpSession session, Packet p)
        {
            var unk = p.ReadUInt32();

            Player plr;
            if (!_players.TryGetValue(session.Guid, out plr))
            {
                session.StopListening();
                return;
            }
            var room = plr.Room;
            if (room == null)
                return;

            var p2pPacketLen = (int)p.ReadUInt16();
            var p2pData = p.ReadBytes(p2pPacketLen);

            var ack = new Packet(ERelayPacket.SDetourPackettAck);
            ack.Write((byte)0x00);
            ack.Write(p2pData);

            var p2pPacket = new P2PPacket(p2pData);
            if (p2pPacket.PacketID == EP2PPacket.PLAYER_SPAWN_REQ || p2pPacket.PacketID == EP2PPacket.PLAYER_SPAWN_ACK)
            {
                room.Broadcast(ack);
                return;
            }

            foreach (var entry in plr.RelayProxies.Select(slot => room.Players.GetPlayerBySlot(slot)).Where(entry => entry != null && entry.Room != null && entry.Room.TunnelID == room.TunnelID))
            {
                entry.Session.Send(ack);
            }
        }
    }
}
