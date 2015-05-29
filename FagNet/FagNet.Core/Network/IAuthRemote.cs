using System.Net;
using System.ServiceModel;

namespace FagNet.Core.Network
{
    [ServiceContract]
    public interface IAuthRemote
    {
        [OperationContract(Name = "ValidateSessionWithSID")]
        bool ValidateSession(uint sessionID, ulong accountID, IPAddress ip);
        [OperationContract(Name = "ValidateSessionWithNick")]
        bool ValidateSession(string nickname, ulong accountID, IPAddress ip);
    }
}
