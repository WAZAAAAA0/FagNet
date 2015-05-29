using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.IO;
using FagNet.Core.Network.Events;


namespace FagNet.Core.Network
{
    public class TcpSession
    {
        private readonly Guid _guid;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public Guid Guid { get { return _guid; } }
        public TcpClient Client { get; private set; }
        public bool IsConnected { get; private set; }

        public event EventHandler<ClientDisconnectedEventArgs> Disconnected;
        public event EventHandler<PacketReceivedEventArgs> PacketReceived;
        public event EventHandler<ExceptionEventArgs> Error;

        private void RaiseDisconnected(ClientDisconnectedEventArgs e)
        {
            if (Disconnected != null)
                Disconnected(this, e);
        }
        private void RaisePacketReceived(PacketReceivedEventArgs e)
        {
            if (PacketReceived != null)
                Task.Factory.FromAsync(PacketReceived.BeginInvoke, PacketReceived.EndInvoke, this, e, null).ContinueWith((t) => { if (t.IsFaulted) RaiseError(new ExceptionEventArgs(t.Exception)); });
                //PacketReceived(this, e);
        }
        private void RaiseError(ExceptionEventArgs e)
        {
            if (Error != null)
                Error(this, e);
        }

        public TcpSession(TcpClient client)
        {
            _guid = Guid.NewGuid();
            Client = client;
            _cancellationTokenSource = new CancellationTokenSource();
            IsConnected = false;
        }

        async public void StartListening()
        {
            if (IsConnected)
                return;
            if (Client == null)
                throw new ObjectDisposedException("TcpClient");

            IsConnected = true;

            while (IsConnected)
            {
                try
                {
                    if (!IsConnected)
                        return;
                    // receive packet length
                    var buffer = new byte[2];
                    var stream = Client.GetStream();

                    var bytesRead = await stream.ReadAsync(buffer, 0, 2, _cancellationTokenSource.Token);

                    if (bytesRead == 0) // client disconnected
                    {
                        StopListening();
                        return;
                    }

                    // receive packet data
                    var size = (int)BitConverter.ToUInt16(buffer, 0);
                    size -= 2;
                    buffer = new byte[size];

                    while (Client.Available < size) Task.Delay(1).Wait();
                    bytesRead = stream.Read(buffer, 0, size);

                    if (bytesRead != size)
                        continue;
                    RaisePacketReceived(new PacketReceivedEventArgs(this, buffer));
                }
                catch (IOException)
                {
                    StopListening();
                    return;
                }
                catch (ObjectDisposedException)
                {
                    StopListening();
                    return;
                }
                catch (Exception e)
                {
                    RaiseError(new ExceptionEventArgs(e));
                    StopListening();
                    return;
                }
            }
        }

        public void StopListening()
        {
            if (!IsConnected)
                return;
            RaiseDisconnected(new ClientDisconnectedEventArgs(this));
            try
            {
                if (Client != null)
                {
                    _cancellationTokenSource.Cancel();
                    Client.Close();
                    Client = null;
                }

                IsConnected = false;
            }
            catch
            { }
            
        }

        public void Send(byte[] data)
        {
            if (!IsConnected)
                return;

            try
            {
                var stream = Client.GetStream();
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
            catch (Exception ex)
            {
                RaiseError(new ExceptionEventArgs(ex));
                StopListening();
            }
        }

        public void Send(Packet packet)
        {
            if (!IsConnected)
                return;
            Send(packet.GetData());
        }
    }
}
