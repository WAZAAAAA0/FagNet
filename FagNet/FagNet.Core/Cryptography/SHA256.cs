using System;
using System.Text;
using System.Security.Cryptography;

namespace FagNet.Core.Cryptography
{
    public static class SHA256
    {
        public static string ComputeHash(string val, Encoding encoding = null)
        {
            if (encoding == null)
                encoding = Encoding.ASCII;

            using (var sha256 = new SHA256CryptoServiceProvider())
            {
                var tmp = sha256.ComputeHash(encoding.GetBytes(val));
                return BitConverter.ToString(tmp).Replace("-", "").ToLower();
            }
        }
    }
}
