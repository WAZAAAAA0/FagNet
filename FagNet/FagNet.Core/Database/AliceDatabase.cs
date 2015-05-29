using System.Collections.Concurrent;
using FagNet.Core.Utils;

namespace FagNet.Core.Database
{
    //public class AliceDatabase : Database
    //{
    //    public static AliceDatabase Instance
    //    { get { return Singleton<AliceDatabase>.Instance; } }

    //    public ConcurrentBag<string> GetWordFilter()
    //    {
    //        var ls = new ConcurrentBag<string>();
    //        using (var con = GetConnection())
    //        {
    //            using (var cmd = con.CreateCommand())
    //            {
    //                cmd.CommandText = "SELECT * FROM word_filter";
    //                using (var r = cmd.ExecuteReader())
    //                {
    //                    while(r.Read())
    //                        ls.Add(r.GetString("Word"));
    //                }
    //            }
    //        }
    //        return ls;
    //    }
    //}
}
