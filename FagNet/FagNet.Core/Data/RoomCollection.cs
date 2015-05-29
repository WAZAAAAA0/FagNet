using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace FagNet.Core.Data
{
    public class RoomCollection : ConcurrentDictionary<uint, Room>
    {
        public Room GetRoomByID(uint channelID, uint roomID)
        {
            var res = from room in Values
                      where room.Channel != null && room.Channel.ID == channelID && room.ID == roomID
                      select room;
            var rooms = res as IList<Room> ?? res.ToList();
            return !rooms.Any() ? null : rooms.First();
        }

        public uint CreateRoomID(uint channelID)
        {
            var res = from room in Values
                      where room.Channel != null && room.Channel.ID == channelID
                      select room;
            var rooms = res as IList<Room> ?? res.ToList();
            uint roomID = 1;

            while (true)
            {
                var res2 = rooms.Where(room => room.ID == roomID);
                if (!res2.Any())
                    return roomID;
                roomID++;
            }
        }

        public uint CreateTunnelID()
        {
            uint tunnelID = 1;
            while (ContainsKey(tunnelID))
                tunnelID++;
            return tunnelID;
        }
    }
}
