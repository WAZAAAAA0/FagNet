using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using FagNet.Core.Constants;

namespace FagNet.Core.Data
{
    public class PlayerCollection : ConcurrentDictionary<Guid, Player>
    {
        public Player GetPlayerByID(ulong accID)
        {
            var res = from plr in Values
                      where plr.AccountID == accID
                      select plr;
            var players = res as IList<Player> ?? res.ToList();
            return !players.Any() ? null : players.First();
        }
        public Player GetPlayerByName(string username)
        {
            var res = from plr in Values
                      where plr.Username.Equals(username)
                      select plr;
            var players = res as IList<Player> ?? res.ToList();
            return !players.Any() ? null : players.First();
        }
        public Player GetPlayerByNickname(string nickname)
        {
            var res = from plr in Values
                      where plr.Nickname.Equals(nickname)
                      select plr;
            var players = res as IList<Player> ?? res.ToList();
            return !players.Any() ? null : players.First();
        }
        public Player GetPlayerBySessionID(uint sessionID)
        {
            var res = from plr in Values
                      where plr.SessionID == sessionID
                      select plr;
            var players = res as IList<Player> ?? res.ToList();
            return !players.Any() ? null : players.First();
        }
        public Player GetPlayerBySlot(byte slot)
        {
            var res = from plr in Values
                      where plr.SlotID == slot
                      select plr;
            var players = res as IList<Player> ?? res.ToList();
            return !players.Any() ? null : players.First();
        }

        public void ChangeState(EPlayerState state)
        {
            foreach (var plr in Values)
                plr.State = state;
        }
    }
}
