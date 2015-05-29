using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using FagNet.Core.Constants;

namespace FagNet.Core.Network
{
    public class RemoteServer
    {
        public Binding Binding { get; private set; }
        public ServiceHost ServiceHost { get; private set; }

        public RemoteServer(object singletonInstance, ERemoteBinding binding, string baseAddress)
        {
            var protocol = "";
            switch (binding)
            {
                case ERemoteBinding.Tcp:
                    Binding = new NetTcpBinding(SecurityMode.None);
                    protocol = "net.tcp://";
                    break;

                case ERemoteBinding.Pipe:
                    Binding = new NetNamedPipeBinding();
                    protocol = "net.pipe://";
                    break;

                case ERemoteBinding.Http:
                    Binding = new BasicHttpBinding(BasicHttpSecurityMode.None);
                    protocol = "http://";
                    break;
            }
            ServiceHost = new ServiceHost(singletonInstance, new Uri(protocol + baseAddress));
        }

        public void Open()
        {
            ServiceHost.Open();
        }

        public void Close()
        {
            ServiceHost.Close();
        }

        public void AddServiceEndpoint(Type type, string name)
        {
            ServiceHost.AddServiceEndpoint(type, Binding, name);
        }
    }
}
