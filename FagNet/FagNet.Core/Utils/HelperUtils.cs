using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FagNet.Core.Utils
{
    public static class HelperUtils
    {
        public static long GetUnixTimestamp()
        {
            return (long)((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds);
        }

        public static long GetUnixTimestamp(DateTime dt)
        {
            return (long)((dt.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds);
        }

        public static DateTime UnixToDateTime(long unix)
        {
            var dt = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            dt = dt.AddSeconds(unix);
            return dt;
        }

        public static string GetS4Color(int r, int g, int b, int a = 255)
        {
            return string.Format("{0}CB-{1},{2},{3},{4}{5}", "{", r, g, b, a, "}");
        }

        public static string[] ParseArgs(string cmd)
        {
            var args = new List<string>();
            using (var r = new StringReader(cmd))
            {
                while (r.Peek() != -1)
                {
                    if (r.Peek() == ' ')
                        r.Read();
                    if (r.Peek() == '\"')
                    {
                        r.Read();
                        var tmp = new StringBuilder();
                        while (r.Peek() != '\"' && r.Peek() != -1)
                            tmp.Append((char)r.Read());

                        r.Read();
                        args.Add(tmp.ToString());
                    }
                    else
                    {
                        var tmp = new StringBuilder();
                        while (r.Peek() != -1 && r.Peek() != ' ')
                            tmp.Append((char)r.Read());
                        r.Read();
                        args.Add(tmp.ToString());
                    }
                }
            }

            return args.ToArray();
        }
    }
}
