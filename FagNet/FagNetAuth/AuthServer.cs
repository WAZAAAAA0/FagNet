using System;
using System.IO;
using System.Net;
using System.ServiceModel;
using FagNet.Core.Constants;
using FagNet.Core.Constants.Packets;
using FagNet.Core.Cryptography;
using FagNet.Core.Database;
using FagNet.Core.Network;
using FagNet.Core.Network.Events;
using FagNet.Core.Utils;

namespace FagNetAuth
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class AuthServer : IAuthRemote
    {
        public static AuthServer Instance { get { return Singleton<AuthServer>.Instance; } }

        private readonly PacketLogger _packetLogger;
        private readonly Logger _logger;

        private readonly TcpServer _server;

        private readonly UDPClient _natServer;
        private readonly UDPClient _natServer2;

        private readonly RemoteServer _remoteServer;

        private readonly SessionCollection _sessions = new SessionCollection();

        public AuthServer()
        {
            _packetLogger = new PacketLogger();
            _logger = new Logger() { WriteToConsole = true };
            _logger.Load(Path.Combine("logs", string.Format("auth_{0}.log", DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss"))));
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Error(s, new ExceptionEventArgs((Exception)e.ExceptionObject));
                Environment.Exit(0);
            };

            _packetLogger.Load("auth_packets.log");

            _logger.Info("Loading auth_config.xml...");
            AuthConfig.Load();

            _logger.Info("Setting up servers...");

            _server = new TcpServer(IPAddress.Parse(AuthConfig.Instance.IP), AuthConfig.Instance.Port);
            _server.PacketReceived += HandlePacket;
            _server.Error += Error;

            _natServer = new UDPClient(38915);
            _natServer.PacketReceived += HandleNATTest;
            _natServer.Error += Error;

            _natServer2 = new UDPClient(38917);
            _natServer2.PacketReceived += HandleNATTest2;
            _natServer2.Error += Error;

            var isMono = Type.GetType("Mono.Runtime") != null;
            switch (AuthConfig.Instance.Remote.Binding)
            {
                case "pipe":
                    if (isMono)
                    {
                        _logger.Error("pipe is not supported in mono, use http!");
                        Environment.Exit(1);
                        return;
                    }
                    _remoteServer = new RemoteServer(this, ERemoteBinding.Pipe, string.Format("localhost/FagNetAuth/{0}/", SHA256.ComputeHash(AuthConfig.Instance.Remote.Password)));
                    break;

                case "tcp":
                    if (isMono)
                    {
                        _logger.Error("tcp is not supported in mono, use http!");
                        Environment.Exit(1);
                        return;
                    }
                    _remoteServer = new RemoteServer(this, ERemoteBinding.Pipe, string.Format("{0}:{1}/FagNetAuth/{2}/", AuthConfig.Instance.Remote.Server, AuthConfig.Instance.Remote.Port, SHA256.ComputeHash(AuthConfig.Instance.Remote.Password)));
                    break;

                case "http":
                    _remoteServer = new RemoteServer(this, ERemoteBinding.Http, string.Format("{0}:{1}/FagNetAuth/{2}/", AuthConfig.Instance.Remote.Server, AuthConfig.Instance.Remote.Port, SHA256.ComputeHash(AuthConfig.Instance.Remote.Password)));
                    break;

                default:
                    _logger.Error("Invalid remote binding '{0}'", AuthConfig.Instance.Remote.Binding);
                    Environment.Exit(1);
                    return;
            }
            _remoteServer.AddServiceEndpoint(typeof(IAuthRemote), "IAuthRemote");
        }

        public void Start()
        {
            _logger.Info("Connecting to MySQL database...");
            try
            {
                AuthDatabase.Instance.TryConnect(AuthConfig.Instance.MySQLAuth.Server, AuthConfig.Instance.MySQLAuth.User, AuthConfig.Instance.MySQLAuth.Password, AuthConfig.Instance.MySQLAuth.Database);
            }
            catch (Exception ex)
            {
                _logger.Error("Could not connect to MySQL database: {0}\r\n{1}",
                    ex.Message, ex.StackTrace);
                Environment.Exit(0);
            }

            _remoteServer.Open();
            _natServer.Start();
            _natServer2.Start();
            _server.Start();
            _logger.Info("Ready for connections!");
        }

        public void Stop()
        {
            _logger.Info("Shutting down...");
            _remoteServer.Close();
            _natServer.Stop();
            _natServer2.Stop();
            _server.Stop();
            _logger.Dispose();
            _packetLogger.Dispose();
        }

        private void HandleNATTest2(object sender, UdpDataReceivedEventArgs e)
        {
            var p = new Packet(e.Packet, 2);
            //_packetLogger.Log<ENATPacket>(p);

            switch (p.PacketID)
            {
                case (byte)ENATPacket.Req3:
                    var addr = p.ReadUInt32();
                    var port = p.ReadUInt16();
                    //_logger.Debug("-NAT Test2- ID: {0} IP: {1} Port: {2} | {3}", p.PacketID, new IPAddress(addr), port, e.IPEndPoint.ToString());
                    
                    var ack = new Packet(ENATPacket.Ack3);
                    ack.Write((uint)e.IPEndPoint.Address.Address);
                    ack.Write((ushort)e.IPEndPoint.Port);
                    _natServer2.Send(e.IPEndPoint, ack);
                    break;

                default:
                    _logger.Warning("-NAT Test2- ID: {0}", p.PacketID);
                    break;

            }
        }
        private void HandleNATTest(object sender, UdpDataReceivedEventArgs e)
        {
            var p = new Packet(e.Packet, 2);
            //_packetLogger.Log<ENATPacket>(p);

            uint addr;
            ushort port;

            Packet ack;
            switch (p.PacketID)
            {
                case (byte)ENATPacket.Req1: // test request
                    addr = p.ReadUInt32();
                    port = p.ReadUInt16();
                    //_logger.Debug("-NAT Test- ID: {0} IP: {1} Port: {2} | {3}", p.PacketID, new IPAddress(addr), port, e.IPEndPoint.ToString());

                    ack = new Packet(ENATPacket.Ack1);
                    ack.Write((uint)e.IPEndPoint.Address.Address);
                    ack.Write((ushort)e.IPEndPoint.Port);
                    _natServer.Send(e.IPEndPoint, ack);
                    break;

                case (byte)ENATPacket.Req2: // firewall/nat type test
                    addr = p.ReadUInt32();
                    port = p.ReadUInt16();
                    //_logger.Debug("-NAT Test- ID: {0} IP: {1} Port: {2} | {3}", p.PacketID, new IPAddress(addr), port, e.IPEndPoint.ToString());

                    ack = new Packet(ENATPacket.Ack2);
                    ack.Write((uint)e.IPEndPoint.Address.Address);
                    ack.Write((ushort)e.IPEndPoint.Port);
                    _natServer2.Send(e.IPEndPoint, ack);
                    break;

                case (byte)ENATPacket.KeepAlive: // keepalive?
                    break;

                default:
                    _logger.Warning("-NAT Test- ID: {0}", p.PacketID);
                    break;

            }
        }

        private void HandlePacket(object sender, PacketReceivedEventArgs e)
        {
            //_packetLogger.Log<EAuthPacket>(e.Packet);

            switch (e.Packet.PacketID)
            {
                case (byte)EAuthPacket.CAuthReq:
                    HandleAuthRequest(e.Session, e.Packet);
                    break;

                case (byte)EAuthPacket.CLoginReq:
                    HandleLoginRequest(e.Session, e.Packet);
                    break;

                default:
                    _logger.Warning("Unkown packet {0}", e.Packet.PacketID.ToString("x2"));
                    break;
            }
        }

        private void Error(object sender, ExceptionEventArgs e)
        {
            _logger.Error(string.Format("{0}\r\n{1}", e.Exception.Message, e.Exception.StackTrace));
            if (e.Exception.InnerException != null)
                _logger.Error(string.Format("{0}\r\n{1}", e.Exception.InnerException.Message, e.Exception.InnerException.StackTrace));
        }

        private void HandleAuthRequest(TcpSession session, Packet p)
        {
            var ip = session.Client.Client.RemoteEndPoint as IPEndPoint;

            var username = p.ReadCStringBuffer(13);
            var password = p.ReadCString();
            password = SHA256.ComputeHash(password);

            var ack = new Packet(EAuthPacket.SAuthAck);

            //_logger.Debug("-C_AUTH_REQ- User: {0}", username);
            if (!AuthDatabase.Instance.ValidateAccount(username, password))
            {
                _logger.Error("-CAuthReq WRONG- User: {0}", username);
                ack.Write((uint)0);
                ack.Write(new byte[12]);
                ack.Write((byte)ELoginResult.AccountError);
                session.Send(ack);
                session.StopListening();
                return;
            }
            if (AuthDatabase.Instance.IsAccountBanned(username))
            {
                _logger.Error("-CAuthReq BANNED- User: {0}", username);
                ack.Write((uint)0);
                ack.Write(new byte[12]);
                ack.Write((byte)ELoginResult.AccountBlocked);
                session.Send(ack);
                session.StopListening();
                return;
            }
            var ssession = _sessions.AddSession(AuthDatabase.Instance.GetAccountID(username), ip.Address);
            _logger.Info("-CAuthReq SUCCESS- User: {0} SessionID: {1}", username, ssession.SessionID);

            ack.Write(ssession.SessionID); // session id
            ack.Write(new byte[12]); // unk
            ack.Write((byte)ELoginResult.OK);
            session.Send(ack);
        }

        private static void HandleLoginRequest(TcpSession session, Packet p)
        {
            var serverList = AuthDatabase.Instance.GetServerList();
            var ack = new Packet(EAuthPacket.SServerlistAck);
            ack.Write((byte)serverList.Count);
            foreach (var server in serverList)
            {
                ack.Write(server.ID);
                ack.Write(server.Type);
                ack.WriteStringBuffer(server.Name, 40);
                ack.Write(server.PlayersOnline);
                ack.Write(server.PlayerLimit);
                ack.Write(server.IP.GetAddressBytes());
                ack.Write(server.Port);
            }
            session.Send(ack);
        }

        public bool ValidateSession(uint sessionID, ulong accountID, IPAddress ip)
        {
            SSession session;
            if (!_sessions.TryGetValue(sessionID, out session)) return false;
            if (session.IP.ToString() == "127.0.0.1") return true;
            return session.AccountID == accountID && session.IP.ToString().Equals(ip.ToString());
        }

        public bool ValidateSession(string nickname, ulong accountID, IPAddress ip)
        {
            var session = _sessions.GetSessionByAccountID(accountID);
            if (session.IP.ToString() == "127.0.0.1") return true;

            if (session.AccountID != accountID)
                return false;
            if (!session.IP.ToString().Equals(ip.ToString()))
                return false;
            if (nickname != AuthDatabase.Instance.GetNickname(accountID))
                return false;
            return accountID == AuthDatabase.Instance.GetAccountIDByNickname(nickname);
        }
    }
}
