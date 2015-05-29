using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using FagNet.Core.Network.Events;

namespace FagNet.Core.Network
{
    public class TcpServer
    {
        private readonly ConcurrentDictionary<Guid, TcpSession> _sessions = new ConcurrentDictionary<Guid, TcpSession>();
        private readonly TcpListener _listener;
        private bool _isListening;

        public event EventHandler<ClientConnectedEventArgs> ClientConnected;
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;
        public event EventHandler<PacketReceivedEventArgs> PacketReceived;
        public event EventHandler<ExceptionEventArgs> Error;

        private void RaiseClientConnected(ClientConnectedEventArgs e)
        {
            if (ClientConnected != null)
                ClientConnected(this, e);
        }
        private void RaiseClientDisconnected(ClientDisconnectedEventArgs e)
        {
            if (ClientDisconnected != null)
                ClientDisconncted(this, e);
        }
        private void RaisePacketReceived(PacketReceivedEventArgs e)
        {
            if (PacketReceived != null)
                PacketReceived(this, e);
        }
        private void RaiseError(ExceptionEventArgs e)
        {
            if (Error != null)
                Error(this, e);
        }

        public TcpServer(IPAddress ip, ushort port)
        {
            if (ip == null)
                throw new NullReferenceException("ip is null");

            _listener = new TcpListener(ip, port);
        }

        async public void Start()
        {
            if (_isListening)
                throw new InvalidOperationException("Server is already listening");

            _listener.Start();
            _isListening = true;

            while (_isListening)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    var session = new TcpSession(client);
                    session.PacketReceived += ClientPacketReceived;
                    session.Disconnected += ClientDisconnected;
                    session.Error += ClientError;

                    if (!_sessions.TryAdd(session.Guid, session)) continue;
                    RaiseClientConnected(new ClientConnectedEventArgs(session));
                    session.StartListening();
                }
                catch (ObjectDisposedException) { break; }
            }
        }

        public void Stop()
        {
            if (!_isListening)
                throw new InvalidOperationException("Server is not listening");

            _listener.Stop();
            foreach (var session in _sessions.Values)
            {
                if (session.IsConnected)
                    session.StopListening();
            }
            _sessions.Clear();
            _isListening = false;
        }

        public void Broadcast(Packet packet, params Guid[] ignore)
        {
            Broadcast(packet.GetData(), ignore);
        }
        public void Broadcast(byte[] packet, params Guid[] ignore)
        {
            if (!_isListening)
                throw new InvalidOperationException("Server is not listening");

            var ls = (ignore == null) ? new List<Guid>() : ignore.ToList();
            foreach (var session in _sessions.Values.Where(session => !ls.Contains(session.Guid)).Where(session => session.IsConnected))
                session.Send(packet);
        }

        public void SendTo(Guid guid, Packet packet)
        {
            SendTo(guid, packet.GetData());
        }
        public void SendTo(Guid guid, byte[] packet)
        {
            TcpSession session;
            if (_sessions.TryGetValue(guid, out session))
                session.Send(packet);
        }

        public TcpSession GetSession(Guid guid)
        {
            TcpSession session;
            return _sessions.TryGetValue(guid, out session) ? session : null;
        }

        protected void ClientDisconncted(object sender, ClientDisconnectedEventArgs e)
        {
            RaiseClientDisconnected(e);
            TcpSession session;
            _sessions.TryRemove(e.Session.Guid, out session);
        }

        protected void ClientPacketReceived(object sender, PacketReceivedEventArgs e)
        {
            RaisePacketReceived(e);
        }

        protected void ClientError(object sender, ExceptionEventArgs e)
        {
            RaiseError(e);
        }
    }
}
