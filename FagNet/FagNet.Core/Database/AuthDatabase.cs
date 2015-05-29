using System.Collections.Generic;
using System.Net;
using FagNet.Core.Utils;
using FagNet.Core.Data;

namespace FagNet.Core.Database
{
    public class AuthDatabase : Database
    {
        public static AuthDatabase Instance
        { get { return Singleton<AuthDatabase>.Instance; } }

        public List<SServer> GetServerList()
        {
            var ls = new List<SServer>();
            using (var con = GetConnection())
            {
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM server";
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var server = new SServer
                            {
                                ID = r.GetUInt16("ID"),
                                Type = r.GetByte("Type"),
                                Name = r.GetString("Name"),
                                PlayerLimit = r.GetUInt16("PlayerLimit"),
                                IP = IPAddress.Parse(r.GetString("IP")),
                                Port = r.GetUInt16("Port")
                            };
                            ls.Add(server);
                        }
                    }
                }
            }

            return ls;
        }

        public bool ValidateAccount(string name, string password)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "SELECT * FROM accounts WHERE Username=@Username AND Password=@Password", "@Username", name, "@Password", password))
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        return r.Read();
                    }
                }
            }
        }

        public bool IsAccountBanned(string name)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "SELECT Banned FROM accounts WHERE Username=@Username", "@Username", name))
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            return false;

                        var banned = r.GetUInt64("Banned");
                        if (banned == 0)
                            return false;

                        var timestamp = (ulong)HelperUtils.GetUnixTimestamp();
                        if (banned >= timestamp) return true;
                        UpdateBannedStatus(name, 0);
                        return false;
                    }
                }
            }
        }
        
        public void UpdateBannedStatus(ulong accID, ulong banned)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "UPDATE accounts SET Banned=@Banned WHERE ID=@ID", "@Banned", banned, "@ID", accID))
                    cmd.ExecuteNonQuery();
            }
        }
        public void UpdateBannedStatus(string username, ulong banned)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "UPDATE accounts SET Banned=@Banned WHERE Username=@Username", "@Banned", banned, "@Username", username))
                    cmd.ExecuteNonQuery();
            }
        }

        public ulong GetAccountID(string username)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "SELECT ID FROM accounts WHERE Username=@Username", "@Username", username))
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        return !r.Read() ? 0 : r.GetUInt64("ID");
                    }
                }
            }
        }

        public ulong GetAccountIDByNickname(string nickname)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "SELECT ID FROM accounts WHERE Nickname=@Nickname", "@Nickname", nickname))
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        return !r.Read() ? 0 : r.GetUInt64("ID");
                    }
                }
            }
        }

        public string GetUsername(ulong id)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "SELECT Username FROM accounts WHERE ID=@ID", "@ID", id))
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        return !r.Read() ? string.Empty : r.GetString("Username");
                    }
                }
            }
        }

        public string GetNickname(ulong id)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "SELECT Nickname FROM accounts WHERE ID=@ID", "@ID", id))
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        return !r.Read() ? string.Empty : r.GetString("Nickname");
                    }
                }
            }
        }

        public byte GetGMLevel(ulong id)
        {
            using (var con = GetConnection())
            {
                using (var cmd = BuildQuery(con, "SELECT GMLevel FROM accounts WHERE ID=@ID", "@ID", id))
                {
                    using (var r = cmd.ExecuteReader())
                    {
                        return (byte) (!r.Read() ? 0 : r.GetByte("GMLevel"));
                    }
                }
            }
        }
    }
}
