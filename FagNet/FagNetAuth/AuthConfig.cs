using FagNet.Core.Utils;

namespace FagNetAuth
{
    public class AuthConfig : SingletonBase<AuthConfig>
    {
        public static void Load()
        {
            _instance = Config<AuthConfig>.Load("auth_config.xml");
        }
        public static void Save()
        {
            Config<AuthConfig>.Save(_instance, "auth_config.xml");
        }

        public string IP { get; set; }
        public ushort Port { get; set; }

        public Remote Remote { get; set; }

        public ConfigMySQL MySQLAuth { get; set; }

        public AuthConfig()
        {
            IP = "0.0.0.0";
            Port = 28002;

            Remote = new Remote() { Binding = "pipe", Server = "127.0.0.1", Port = 27001, Password = "hekker" };
            MySQLAuth = new ConfigMySQL { Database = "auth" };
        }
    }
}
