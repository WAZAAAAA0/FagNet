using System;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using FagNet.Core.Network.Events;

namespace FagNet.Core.Network
{
    public class UDPClient
    {
        public UdpClient Client { get; private set; }

        private bool _isListening;

        public event EventHandler<UdpDataReceivedEventArgs> PacketReceived;
        public event EventHandler<ExceptionEventArgs> Error;

        protected void RaisePacketReceived(UdpDataReceivedEventArgs e)
        {
            if (PacketReceived != null)
                Task.Factory.StartNew(() => PacketReceived.Invoke(this, e)).ContinueWith((t) => { if(t.IsFaulted) RaiseError(new ExceptionEventArgs(t.Exception));});
        }
        protected void RaiseError(ExceptionEventArgs e)
        {
            if (Error != null)
                Error(this, e);
        }

        public UDPClient(ushort port)
        {
            Client = new UdpClient(port);
        }

        async public void Start()
        {
            if (_isListening)
                throw new InvalidOperationException("Server is already listening");

            _isListening = true;

            while (_isListening)
            {
                try
                {
                    var result = await Client.ReceiveAsync();
                    RaisePacketReceived(new UdpDataReceivedEventArgs(result.RemoteEndPoint, result.Buffer));
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex) { Console.WriteLine(ex.Message + "\r\n" + ex.StackTrace); }
            }
        }

        public void Stop()
        {
            if (!_isListening)
                throw new InvalidOperationException("Server is not listening");

            Client.Close();
            _isListening = false;
        }

        public void Send(IPEndPoint ip, Packet packet)
        {
            Send(ip, packet.GetData());
        }
        public void Send(IPEndPoint ip, byte[] packet)
        {
            try
            {
                Client.Send(packet, packet.Length, ip);
            }
            catch (Exception ex)
            {
                //RaiseError(new ExceptionEventArgs(ex));
            }
        }

        protected void ClientError(object sender, ExceptionEventArgs e)
        {
            RaiseError(e);
        }
    }
}
