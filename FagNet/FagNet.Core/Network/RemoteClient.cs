using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using FagNet.Core.Constants;

namespace FagNet.Core.Network
{
    public class RemoteClient
    {
        private readonly Binding _binding;
        private readonly Uri _baseAddress;

        public RemoteClient(ERemoteBinding binding, string baseAddress)
        {
            var protocol = "";
            switch (binding)
            {
                case ERemoteBinding.Tcp:
                    _binding = new NetTcpBinding(SecurityMode.None);
                    protocol = "net.tcp://";
                    break;

                case ERemoteBinding.Pipe:
                    _binding = new NetNamedPipeBinding();
                    protocol = "net.pipe://";
                    break;

                case ERemoteBinding.Http:
                    _binding = new BasicHttpBinding(BasicHttpSecurityMode.None);
                    protocol = "http://";
                    break;
            }
            _baseAddress = new Uri(protocol + baseAddress);
        }

        public T CreateChannel<T>(string name) where T : class
        {
            object obj = null;
            try
            {
                    obj = ChannelFactory<T>.CreateChannel(_binding, new EndpointAddress(new Uri(_baseAddress, name)));
            }
            catch (Exception ex)
            {
                if (obj != null)
                    ((IClientChannel)obj).Abort();
                return default(T);
            }
            return (T)obj;
        }

        public void CloseChannel(object channel)
        {
            try
            { ((IClientChannel)channel).Close(); }
            catch (Exception)
            { ((IClientChannel)channel).Abort(); }
        }
    }
}
