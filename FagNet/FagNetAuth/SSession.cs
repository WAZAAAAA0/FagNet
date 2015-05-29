using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace FagNetAuth
{
    struct SSession
    {
        public ulong AccountID { get; set; }
        public uint SessionID { get; set; }
        public IPAddress IP { get; set; }
    }

    class SessionCollection : ConcurrentDictionary<uint, SSession>
    {
        public SSession AddSession(ulong accID, IPAddress ip)
        {
            var g = Guid.NewGuid();
            var hashcode = (uint)g.GetHashCode();
            while (ContainsKey(hashcode))
            {
                g = Guid.NewGuid();
                hashcode = (uint)g.GetHashCode();
            }

            var session = new SSession {AccountID = accID, SessionID = hashcode, IP = ip};

            if(ContainsAccountID(accID))
            {
                var s = GetSessionByAccountID(accID);
                TryRemove(s.SessionID, out s);
            }
            TryAdd(hashcode, session);
            
            return session;
        }

        public bool ContainsAccountID(ulong accID)
        {
            var res = from session in Values
                      where session.AccountID == accID
                      select session;
            return res.Any();
        }

        public SSession GetSessionByAccountID(ulong accID)
        {
            var res = from session in Values
                      where session.AccountID == accID
                      select session;
            var sessions = res as IList<SSession> ?? res.ToList();
            return !sessions.Any() ? new SSession() : sessions.First();
        }
    }
}
